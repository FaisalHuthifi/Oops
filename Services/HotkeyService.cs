using System.Runtime.InteropServices;
using Oops.Models;

namespace Oops.Services;

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public IntPtr TargetWindow { get; init; }
}

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private const int WmAppConvert = 0x8001;

    private readonly NativeWindowHost _host;
    private HotkeyConfiguration _configuration;
    private bool _registered;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public IntPtr WindowHandle => _host.Handle;

    public HotkeyService(HotkeyConfiguration configuration)
    {
        _configuration = configuration;
        _host = new NativeWindowHost(WndProc);
        Register();
    }

    public void UpdateHotkey(HotkeyConfiguration configuration)
    {
        Unregister();
        _configuration = configuration;
        Register();
    }

    private void Register()
    {
        _registered = RegisterHotKey(
            _host.Handle,
            HotkeyId,
            _configuration.Modifiers,
            _configuration.VirtualKey);

        if (!_registered)
        {
            System.Diagnostics.Debug.WriteLine(
                $"RegisterHotKey failed for modifiers={_configuration.Modifiers} vk={_configuration.VirtualKey}");
        }
    }

    private void Unregister()
    {
        if (!_registered)
            return;

        UnregisterHotKey(_host.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeConstants.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            var targetWindow = GetForegroundWindow();
            PostMessage(hwnd, WmAppConvert, targetWindow, IntPtr.Zero);
            return IntPtr.Zero;
        }

        if (msg == WmAppConvert)
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
            {
                TargetWindow = wParam
            });
            return IntPtr.Zero;
        }

        return NativeWindowHost.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
        _host.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
