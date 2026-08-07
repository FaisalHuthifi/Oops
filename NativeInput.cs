using System.Runtime.InteropServices;

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

    internal static void TryWmCopy(IntPtr targetWindow)
    {
        TryFocusWindow(targetWindow);
        var hwnd = ResolveMessageTarget(targetWindow);
        if (hwnd != IntPtr.Zero)
            SendMessageCrossThread(hwnd, NativeConstants.WM_COPY);
    }

    internal static void TryWmPaste(IntPtr targetWindow)
    {
        TryFocusWindow(targetWindow);
        var hwnd = ResolveMessageTarget(targetWindow);
        if (hwnd != IntPtr.Zero)
            SendMessageCrossThread(hwnd, NativeConstants.WM_PASTE);
    }

    internal static void SendMessageCrossThread(IntPtr hwnd, int message) =>
        WithThreadInput(hwnd, () => SendMessage(hwnd, message, IntPtr.Zero, IntPtr.Zero));

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

    private static void TryFocusWindow(IntPtr targetWindow)
    {
        if (targetWindow != IntPtr.Zero)
            SetForegroundWindow(targetWindow);
    }

    private static IntPtr ResolveMessageTarget(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
            return IntPtr.Zero;

        var focused = GetFocusedWindow(targetWindow);
        return focused != IntPtr.Zero ? focused : targetWindow;
    }

    private static void WithThreadInput(IntPtr hwnd, Action action)
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

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
    private static extern IntPtr SendMessageReplace(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

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
