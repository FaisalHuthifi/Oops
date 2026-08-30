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
        className is "Edit" or "TEdit" or "TMemo" or "ThunderRT6TextBox" || IsRichEdit(className);

    internal static bool IsRichEdit(string className) =>
        className is "RichEdit" or "RichEdit20A" or "RichEdit20W" or "RICHEDIT50W" or "RichEditD2DPT";

    internal static bool ContainsText(IntPtr hwnd, string text)
    {
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(text))
            return false;

        return ReadClassicEditText(hwnd)?.Contains(text, StringComparison.Ordinal) == true;
    }

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
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(newText))
            return false;

        SetSelectionRange(hwnd, className, selStart, selEnd);

        if (IsRichEdit(className))
            NativeInput.SendMessageCrossThread(hwnd, RichReplaceSel, (IntPtr)1, newText);
        else
            NativeInput.SendMessageCrossThread(hwnd, EditReplaceSel, (IntPtr)1, newText);

        SetSelectionRange(hwnd, className, selStart, selStart + newText.Length);
        var verify = ReadSelection(hwnd, className);
        return verify is { Text: var actual } && string.Equals(actual, newText, StringComparison.Ordinal);
    }

    private static (int Start, int End)? GetSelectionRange(IntPtr hwnd, string className)
    {
        if (IsRichEdit(className))
        {
            var start = 0;
            var end = 0;
            NativeInput.SendMessageRefCrossThread(hwnd, RichGetSel, ref start, ref end);
            return (start, end);
        }

        var sel = NativeInput.SendMessageIntCrossThread(hwnd, EditGetSel, IntPtr.Zero, IntPtr.Zero).ToInt32();
        return (sel & 0xFFFF, (sel >> 16) & 0xFFFF);
    }

    private static void SetSelectionRange(IntPtr hwnd, string className, int start, int end)
    {
        if (IsRichEdit(className))
        {
            var s = start;
            var e = end;
            NativeInput.SendMessageRefCrossThread(hwnd, RichSetSel, ref s, ref e);
            return;
        }

        NativeInput.SendMessageIntCrossThread(hwnd, EditSetSel, (IntPtr)start, (IntPtr)end);
    }

    private static string? ReadRichEditSelectedText(IntPtr hwnd, int selectedLength)
    {
        if (selectedLength <= 0)
            return null;

        var buffer = new StringBuilder(selectedLength + 2);
        NativeInput.SendMessageTextCrossThread(hwnd, RichGetSelText, (IntPtr)(selectedLength + 1), buffer);
        return buffer.ToString();
    }

    private static string? ReadClassicEditText(IntPtr hwnd)
    {
        var length = (int)NativeInput.SendMessageIntCrossThread(hwnd, EditGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0)
            return string.Empty;

        var buffer = new StringBuilder(length + 1);
        NativeInput.SendMessageTextCrossThread(hwnd, EditGetText, (IntPtr)(length + 1), buffer);
        return buffer.ToString();
    }
}

internal readonly record struct SelectionReadResult(string Text, int SelStart, int SelEnd);
