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
        var focused = GetFocusedWindow(targetWindow);
        if (focused != IntPtr.Zero)
            SendMessage(focused, NativeConstants.WM_COPY, IntPtr.Zero, IntPtr.Zero);
    }

    internal static void TryWmPaste(IntPtr targetWindow)
    {
        var focused = GetFocusedWindow(targetWindow);
        if (focused != IntPtr.Zero)
            SendMessage(focused, NativeConstants.WM_PASTE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Wait for hotkey keys to release naturally — never inject key events.</summary>
    internal static void WaitForHotkeyRelease()
    {
        ushort[] keys =
        [
            NativeConstants.VK_CONTROL,
            NativeConstants.VK_SHIFT,
            NativeConstants.VK_MENU,
            0x49
        ];

        for (var i = 0; i < 30; i++)
        {
            var anyHeld = keys.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
            if (!anyHeld)
                return;

            Thread.Sleep(10);
        }
    }

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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
