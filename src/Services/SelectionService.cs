using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace Oops.Services;

public sealed record SelectionInfo
{
    public required string Text { get; init; }
    public IntPtr EditHandle { get; init; }
    public string EditClassName { get; init; } = "";
    public int SelStart { get; init; }
    public int SelEnd { get; init; }
    public AutomationElement? AutomationTarget { get; init; }
    public IntPtr TargetWindow { get; init; }

    public bool TryReplace(string newText)
    {
        if (EditHandle != IntPtr.Zero &&
            !string.IsNullOrEmpty(EditClassName) &&
            EditControlHelper.TryReplace(EditHandle, EditClassName, SelStart, SelEnd, newText))
        {
            return true;
        }

        // Typing over the live selection works in browsers, Electron apps, Office
        // and anything else that accepts keyboard input, without the clipboard.
        if (TryTypeOverSelection(newText))
            return true;

        return AutomationTarget != null &&
               AutomationHelper.TryReplaceSelection(AutomationTarget, Text, newText);
    }

    private bool TryTypeOverSelection(string newText)
    {
        var window = TargetWindow != IntPtr.Zero ? TargetWindow : EditHandle;
        if (!NativeInput.IsForeground(window))
            return false;

        if (AutomationTarget != null && AutomationHelper.IsReadOnly(AutomationTarget))
            return false;

        if (!NativeInput.SendUnicodeText(newText))
            return false;

        return VerifyTyped(newText);
    }

    private bool VerifyTyped(string newText)
    {
        // Give the target app time to process the synthesized keystrokes.
        for (var attempt = 0; attempt < 12; attempt++)
        {
            Thread.Sleep(25);

            if (EditHandle != IntPtr.Zero && EditControlHelper.ContainsText(EditHandle, newText))
                return true;

            if (AutomationTarget == null)
                continue;

            var confirmed = AutomationHelper.TryConfirmText(AutomationTarget, newText);
            if (confirmed == true)
                return true;

            // The surface exposes no readable text, so the keystrokes cannot be
            // verified; a dispatched batch is the best signal available.
            if (confirmed == null)
                return true;
        }

        return false;
    }
}

public sealed class SelectionService
{
    private readonly IntPtr _ownWindowHandle;

    public SelectionService(IntPtr ownWindowHandle) =>
        _ownWindowHandle = ownWindowHandle;

    public IntPtr ResolveTargetWindow(IntPtr capturedWindow)
    {
        if (capturedWindow == _ownWindowHandle)
            capturedWindow = IntPtr.Zero;

        if (capturedWindow == IntPtr.Zero)
            capturedWindow = NativeInput.GetForegroundWindow();

        if (capturedWindow == _ownWindowHandle)
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
            var fromFocused = TryGetWin32EditSelection(focused);
            if (fromFocused != null)
                return fromFocused;
        }

        var fromTarget = TryFindEditSelectionInWindow(targetWindow);
        if (fromTarget != null)
            return fromTarget;

        return TryGetSelectionViaAutomation(targetWindow);
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
                    return ToWin32Selection(selection, current, className);
            }

            var child = FindEditChildDeep(current);
            if (child == IntPtr.Zero)
                continue;

            var childClass = ReadWindowClassName(child);
            var childRead = EditControlHelper.ReadSelection(child, childClass);
            if (childRead is { } childSelection)
                return ToWin32Selection(childSelection, child, childClass);
        }

        return null;
    }

    private static SelectionInfo? TryFindEditSelectionInWindow(IntPtr targetWindow)
    {
        var edit = FindEditChildDeep(targetWindow);
        if (edit == IntPtr.Zero)
            return null;

        var className = ReadWindowClassName(edit);
        var read = EditControlHelper.ReadSelection(edit, className);
        return read is { } selection ? ToWin32Selection(selection, edit, className) : null;
    }

    private static SelectionInfo? TryGetSelectionViaAutomation(IntPtr targetWindow)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null)
                return null;

            foreach (var element in WalkElements(focused))
            {
                var text = AutomationHelper.ReadSelection(element);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var hwnd = new IntPtr(element.Current.NativeWindowHandle);
                if (hwnd != IntPtr.Zero)
                {
                    var className = ReadWindowClassName(hwnd);
                    if (EditControlHelper.IsEditClassName(className))
                    {
                        var read = EditControlHelper.ReadSelection(hwnd, className);
                        if (read is { } win32Selection &&
                            string.Equals(win32Selection.Text, text, StringComparison.Ordinal))
                        {
                            return ToWin32Selection(win32Selection, hwnd, className);
                        }
                    }
                }

                return new SelectionInfo
                {
                    Text = text,
                    AutomationTarget = element,
                    TargetWindow = targetWindow
                };
            }
        }
        catch
        {
            // Some apps block UI Automation.
        }

        return null;
    }

    private static IEnumerable<AutomationElement> WalkElements(AutomationElement start)
    {
        yield return start;

        var walker = TreeWalker.ControlViewWalker;
        for (var parent = walker.GetParent(start); parent != null; parent = walker.GetParent(parent))
            yield return parent;
    }

    private static SelectionInfo ToWin32Selection(
        SelectionReadResult read,
        IntPtr handle,
        string className) =>
        new()
        {
            Text = read.Text,
            EditHandle = handle,
            EditClassName = className,
            SelStart = read.SelStart,
            SelEnd = read.SelEnd,
            TargetWindow = handle
        };

    private static IntPtr FindEditChildDeep(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (child, _) =>
        {
            var className = ReadWindowClassName(child);
            if (EditControlHelper.IsEditClassName(className))
            {
                found = child;
                return false;
            }

            var nested = FindEditChildDeep(child);
            if (nested != IntPtr.Zero)
            {
                found = nested;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
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
    private static extern IntPtr GetParent(IntPtr hWnd);
}
