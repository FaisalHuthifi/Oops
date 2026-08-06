using System.Runtime.InteropServices;
using System.Text;

namespace Oops.Services;

internal static class EditControlHelper
{
    private const int WmUser = 0x0400;

    private const int EditGetSel = 0xB0;
    private const int EditSetSel = 0xB1;
    private const int EditReplaceSel = 0xC2;
    private const int EditGetText = 0xD;
    private const int EditGetTextLength = 0xE;

    private const int RichGetSel = WmUser;
    private const int RichSetSel = WmUser + 1;
    private const int RichReplaceSel = WmUser + 2;
    private const int RichGetSelText = WmUser + 75;

    internal static bool IsEditClassName(string className) =>
        className is "Edit" or "RICHEDIT50W" or "RichEditD2DPT";

    internal static bool IsRichEdit(string className) =>
        className is "RICHEDIT50W" or "RichEditD2DPT";

    internal static SelectionReadResult? ReadSelection(IntPtr hwnd, string className)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        var range = GetSelectionRange(hwnd, className);
        if (range is not { } r || r.Start == r.End)
            return null;

        if (IsRichEdit(className))
        {
            var selected = ReadRichEditSelectedText(hwnd, r.End - r.Start);
            return string.IsNullOrEmpty(selected) ? null : new SelectionReadResult(selected, r.Start, r.End);
        }

        var fullText = ReadClassicEditText(hwnd);
        if (fullText is null || r.Start < 0 || r.End > fullText.Length || r.Start >= r.End)
            return null;

        return new SelectionReadResult(fullText[r.Start..r.End], r.Start, r.End);
    }

    internal static bool TryReplace(IntPtr hwnd, string className, int selStart, int selEnd, string newText)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        SetSelectionRange(hwnd, className, selStart, selEnd);

        if (IsRichEdit(className))
        {
            _ = SendMessageReplace(hwnd, RichReplaceSel, (IntPtr)1, newText);
            return true;
        }

        _ = SendMessageReplace(hwnd, EditReplaceSel, (IntPtr)1, newText);
        return true;
    }

    private static (int Start, int End)? GetSelectionRange(IntPtr hwnd, string className)
    {
        if (IsRichEdit(className))
        {
            var start = 0;
            var end = 0;
            SendMessageRef(hwnd, RichGetSel, ref start, ref end);
            return (start, end);
        }

        var sel = SendMessageInt(hwnd, EditGetSel, IntPtr.Zero, IntPtr.Zero).ToInt32();
        return (sel & 0xFFFF, (sel >> 16) & 0xFFFF);
    }

    private static void SetSelectionRange(IntPtr hwnd, string className, int start, int end)
    {
        if (IsRichEdit(className))
        {
            var s = start;
            var e = end;
            SendMessageRef(hwnd, RichSetSel, ref s, ref e);
            return;
        }

        _ = SendMessageInt(hwnd, EditSetSel, (IntPtr)start, (IntPtr)end);
    }

    private static string? ReadRichEditSelectedText(IntPtr hwnd, int selectedLength)
    {
        if (selectedLength <= 0)
            return null;

        var buffer = new StringBuilder(selectedLength + 2);
        SendMessageText(hwnd, RichGetSelText, (IntPtr)(selectedLength + 1), buffer);
        return buffer.ToString();
    }

    private static string? ReadClassicEditText(IntPtr hwnd)
    {
        var length = (int)SendMessageInt(hwnd, EditGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0)
            return string.Empty;

        var buffer = new StringBuilder(length + 1);
        SendMessageText(hwnd, EditGetText, (IntPtr)(length + 1), buffer);
        return buffer.ToString();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageInt(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageText(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageReplace(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageRef(IntPtr hWnd, int msg, ref int wParam, ref int lParam);
}

internal readonly record struct SelectionReadResult(string Text, int SelStart, int SelEnd);
