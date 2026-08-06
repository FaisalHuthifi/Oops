using System.Runtime.InteropServices;
using System.Windows.Interop;
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

    private readonly HwndSource _source;
    private HotkeyConfiguration _configuration;
    private bool _registered;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public HotkeyService(HwndSource source, HotkeyConfiguration configuration)
    {
        _source = source;
        _configuration = configuration;
        _source.AddHook(WndProc);
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
            _source.Handle,
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

        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeConstants.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            // Capture target window immediately — before deferred handler runs.
            var targetWindow = GetForegroundWindow();
            PostMessage(hwnd, WmAppConvert, targetWindow, IntPtr.Zero);
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WmAppConvert)
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
            {
                TargetWindow = wParam
            });
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
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
