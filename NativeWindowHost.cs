using System.Runtime.InteropServices;

namespace Oops;

/// <summary>
/// Message-only Win32 window. Avoids WPF HwndSource, which crashes in single-file publishes.
/// </summary>
internal sealed class NativeWindowHost : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly string WindowClass = "OopsNativeHost_" + Guid.NewGuid().ToString("N")[..8];
    private static readonly object RegisterLock = new();
    private static bool _classRegistered;

    private readonly WndProc _wndProc;
    private readonly Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> _handler;
    private IntPtr _hwnd;

    public NativeWindowHost(Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> handler)
    {
        _handler = handler;
        _wndProc = WindowProcedure;
        _hwnd = CreateMessageOnlyWindow();
    }

    public IntPtr Handle => _hwnd;

    private IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) =>
        _handler(hWnd, msg, wParam, lParam);

    private IntPtr CreateMessageOnlyWindow()
    {
        EnsureWindowClassRegistered();

        var hwnd = CreateWindowExW(
            0,
            WindowClass,
            "",
            0,
            0, 0, 0, 0,
            HwndMessage,
            IntPtr.Zero,
            GetModuleHandleW(null),
            IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        return hwnd;
    }

    private void EnsureWindowClassRegistered()
    {
        lock (RegisterLock)
        {
            if (_classRegistered)
                return;

            var wc = new WNDCLASSW
            {
                lpfnWndProc = _wndProc,
                hInstance = GetModuleHandleW(null),
                lpszClassName = WindowClass
            };

            if (RegisterClassW(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410) // 1410 = class already exists
                throw new InvalidOperationException($"RegisterClass failed: {Marshal.GetLastWin32Error()}");

            _classRegistered = true;
        }
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
