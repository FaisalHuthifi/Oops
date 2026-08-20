using System.Runtime.InteropServices;
using System.Text;

namespace Oops;

internal static class NativeInput
{
    internal static IntPtr GetFocusedWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
            return IntPtr.Zero;

        var threadId = GetWindowThreadProcessId(targetWindow, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(threadId, ref info) ? info.hwndFocus : IntPtr.Zero;
    }

    internal static IntPtr SendMessageIntCrossThread(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam)
    {
        IntPtr result = IntPtr.Zero;
        WithThreadInput(hwnd, () => result = SendMessage(hwnd, message, wParam, lParam));
        return result;
    }

    internal static void SendMessageTextCrossThread(IntPtr hwnd, int message, IntPtr wParam, StringBuilder lParam) =>
        WithThreadInput(hwnd, () => SendMessageText(hwnd, message, wParam, lParam));

    internal static void SendMessageRefCrossThread(IntPtr hwnd, int message, ref int wParam, ref int lParam)
    {
        var w = wParam;
        var l = lParam;
        WithThreadInput(hwnd, () => SendMessageRef(hwnd, message, ref w, ref l));
        wParam = w;
        lParam = l;
    }

    internal static void SendMessageCrossThread(IntPtr hwnd, int message, IntPtr wParam, string lParam) =>
        WithThreadInput(hwnd, () => SendMessageReplace(hwnd, message, wParam, lParam));

    internal static void WaitForHotkeyRelease()
    {
        ushort[] keys =
        [
            NativeConstants.VK_CONTROL,
            NativeConstants.VK_SHIFT,
            NativeConstants.VK_MENU,
            0x49
        ];

        for (var i = 0; i < 40; i++)
        {
            var anyHeld = keys.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
            if (!anyHeld)
                return;

            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Types text as Unicode key events, replacing whatever is selected in the
    /// focused control. Works in any surface that accepts keyboard input.
    /// </summary>
    internal static bool SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var inputs = new INPUT[text.Length * 2];

        for (var i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = CreateUnicodeInput(text[i], keyUp: false);
            inputs[i * 2 + 1] = CreateUnicodeInput(text[i], keyUp: true);
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    internal static IntPtr GetRootWindow(IntPtr hwnd) =>
        hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GA_ROOT);

    internal static bool IsForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        var foreground = GetRootWindow(GetForegroundWindow());
        return foreground != IntPtr.Zero && foreground == GetRootWindow(hwnd);
    }

    private static INPUT CreateUnicodeInput(char character, bool keyUp) =>
        new()
        {
            type = INPUT_KEYBOARD,
            union = new INPUTUNION
            {
                keyboard = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = character,
                    dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0u),
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

    internal static void WithThreadInput(IntPtr hwnd, Action action)
    {
        var targetThreadId = GetWindowThreadProcessId(hwnd, out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = false;

        if (targetThreadId != currentThreadId)
            attached = AttachThreadInput(currentThreadId, targetThreadId, true);

        try
        {
            action();
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThreadId, targetThreadId, false);
        }
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInput);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageText(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageReplace(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageRef(IntPtr hWnd, int msg, ref int wParam, ref int lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
