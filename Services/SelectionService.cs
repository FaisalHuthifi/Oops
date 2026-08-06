using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace Oops.Services;

public enum SelectionSource
{
    Win32Edit,
    Clipboard
}

public sealed record SelectionInfo
{
    public required string Text { get; init; }
    public required SelectionSource Source { get; init; }
    public IntPtr EditHandle { get; init; }
    public string EditClassName { get; init; } = "";
    public int SelStart { get; init; }
    public int SelEnd { get; init; }
    public IntPtr TargetWindow { get; init; }

    public bool TryReplace(string newText, ClipboardService clipboard)
    {
        if (Source == SelectionSource.Win32Edit && EditHandle != IntPtr.Zero)
        {
            return EditControlHelper.TryReplace(
                EditHandle,
                EditClassName,
                SelStart,
                SelEnd,
                newText);
        }

        var snapshot = clipboard.Capture();

        try
        {
            if (!clipboard.TrySetText(newText))
                return false;

            Thread.Sleep(40);
            NativeInput.TryWmPaste(TargetWindow);
            Thread.Sleep(20);
            return true;
        }
        finally
        {
            clipboard.Restore(snapshot);
        }
    }
}

public sealed class SelectionService
{
    private readonly ClipboardService _clipboard;
    private readonly uint _ownProcessId;
    private readonly IntPtr _ownWindowHandle;

    public SelectionService(ClipboardService clipboard, IntPtr ownWindowHandle)
    {
        _clipboard = clipboard;
        _ownWindowHandle = ownWindowHandle;
        _ownProcessId = (uint)Process.GetCurrentProcess().Id;
    }

    public IntPtr ResolveTargetWindow(IntPtr capturedWindow)
    {
        if (IsOwnWindow(capturedWindow))
            capturedWindow = IntPtr.Zero;

        if (capturedWindow == IntPtr.Zero)
            capturedWindow = GetForegroundWindow();

        if (IsOwnWindow(capturedWindow))
            return IntPtr.Zero;

        return capturedWindow;
    }

    public SelectionInfo? TryGetSelection(IntPtr targetWindow)
    {
        NativeInput.WaitForHotkeyRelease();

        targetWindow = ResolveTargetWindow(targetWindow);
        if (targetWindow == IntPtr.Zero)
            return null;

        var focused = NativeInput.GetFocusedWindow(targetWindow);

        if (focused != IntPtr.Zero)
        {
            var win32 = TryGetWin32EditSelection(focused);
            if (win32 != null)
                return win32 with { TargetWindow = targetWindow };
        }

        var childWin32 = TryGetWin32EditSelection(targetWindow);
        if (childWin32 != null)
            return childWin32 with { TargetWindow = targetWindow };

        var automationText = TryGetAutomationSelection();
        if (!string.IsNullOrWhiteSpace(automationText))
        {
            return new SelectionInfo
            {
                Text = automationText,
                Source = SelectionSource.Clipboard,
                TargetWindow = targetWindow
            };
        }

        var clipboardText = TryGetSelectionViaClipboard(targetWindow);
        if (string.IsNullOrWhiteSpace(clipboardText))
            return null;

        return new SelectionInfo
        {
            Text = clipboardText,
            Source = SelectionSource.Clipboard,
            TargetWindow = targetWindow
        };
    }

    private SelectionInfo? TryGetWin32EditSelection(IntPtr hwnd)
    {
        for (var current = hwnd; current != IntPtr.Zero; current = GetParent(current))
        {
            var className = ReadWindowClassName(current);
            if (EditControlHelper.IsEditClassName(className))
            {
                var read = EditControlHelper.ReadSelection(current, className);
                if (read is { } selection)
                {
                    return new SelectionInfo
                    {
                        Text = selection.Text,
                        Source = SelectionSource.Win32Edit,
                        EditHandle = current,
                        EditClassName = className,
                        SelStart = selection.SelStart,
                        SelEnd = selection.SelEnd
                    };
                }
            }

            var child = FindEditChild(current);
            if (child == IntPtr.Zero)
                continue;

            var childClass = ReadWindowClassName(child);
            var childRead = EditControlHelper.ReadSelection(child, childClass);
            if (childRead is { } childSelection)
            {
                return new SelectionInfo
                {
                    Text = childSelection.Text,
                    Source = SelectionSource.Win32Edit,
                    EditHandle = child,
                    EditClassName = childClass,
                    SelStart = childSelection.SelStart,
                    SelEnd = childSelection.SelEnd
                };
            }
        }

        return null;
    }

    private static IntPtr FindEditChild(IntPtr parent)
    {
        IntPtr result = IntPtr.Zero;
        EnumChildWindows(parent, (child, _) =>
        {
            if (!EditControlHelper.IsEditClassName(ReadWindowClassNameStatic(child)))
                return true;

            result = child;
            return false;
        }, IntPtr.Zero);

        return result;
    }

    private static string ReadWindowClassNameStatic(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? TryGetAutomationSelection()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null)
                return null;

            var fromFocused = ReadAutomationSelection(focused);
            if (!string.IsNullOrWhiteSpace(fromFocused))
                return fromFocused;

            var walker = TreeWalker.ControlViewWalker;
            for (var parent = walker.GetParent(focused); parent != null; parent = walker.GetParent(parent))
            {
                var fromParent = ReadAutomationSelection(parent);
                if (!string.IsNullOrWhiteSpace(fromParent))
                    return fromParent;
            }
        }
        catch
        {
            // Some apps block UI Automation.
        }

        return null;
    }

    private static string? ReadAutomationSelection(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject))
            return null;

        var textPattern = (TextPattern)patternObject;
        var ranges = textPattern.GetSelection();
        if (ranges == null || ranges.Length == 0)
            return null;

        var text = ranges[0].GetText(-1);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private const string CopyProbeMarker = "\uFDD0\uFDD1";

    private string? TryGetSelectionViaClipboard(IntPtr targetWindow)
    {
        var snapshot = _clipboard.Capture();

        try
        {
            // Place a unique marker so we can tell whether WM_COPY actually copied a selection.
            if (!_clipboard.TrySetText(CopyProbeMarker))
                return null;

            Thread.Sleep(20);
            NativeInput.TryWmCopy(targetWindow);

            for (var attempt = 0; attempt < 8; attempt++)
            {
                Thread.Sleep(30);
                var text = _clipboard.TryGetText();
                if (!string.IsNullOrWhiteSpace(text) &&
                    !string.Equals(text, CopyProbeMarker, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }
        finally
        {
            _clipboard.Restore(snapshot);
        }
    }

    private bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        if (hwnd == _ownWindowHandle)
            return true;

        GetWindowThreadProcessId(hwnd, out var processId);
        return processId == _ownProcessId;
    }

    private static string ReadWindowClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWnd, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
