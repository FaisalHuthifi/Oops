using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
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

    private readonly HotkeyMessageWindow _window;
    private HotkeyConfiguration _configuration;
    private bool _registered;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public IntPtr WindowHandle => _window.Handle;

    public bool IsRegistered => _registered;

    public HotkeyService(HotkeyConfiguration configuration)
    {
        _configuration = configuration;
        _window = new HotkeyMessageWindow(OnHotkeyPressed, OnConvertMessage);
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
            _window.Handle,
            HotkeyId,
            _configuration.Modifiers,
            _configuration.VirtualKey);
    }

    private void Unregister()
    {
        if (!_registered)
            return;

        UnregisterHotKey(_window.Handle, HotkeyId);
        _registered = false;
    }

    private void OnHotkeyPressed()
    {
        var targetWindow = GetForegroundWindow();
        PostMessage(_window.Handle, WmAppConvert, targetWindow, IntPtr.Zero);
    }

    private void OnConvertMessage(IntPtr targetWindow)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.Invoke(() =>
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
            {
                TargetWindow = targetWindow
            });
        });
    }

    public void Dispose()
    {
        Unregister();
        _window.Dispose();
    }

    private sealed class HotkeyMessageWindow : NativeWindow, IDisposable
    {
        private readonly Action _onHotkey;
        private readonly Action<IntPtr> _onConvert;

        public HotkeyMessageWindow(Action onHotkey, Action<IntPtr> onConvert)
        {
            _onHotkey = onHotkey;
            _onConvert = onConvert;

            CreateHandle(new CreateParams
            {
                Parent = (IntPtr)(-3) // HWND_MESSAGE
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeConstants.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                _onHotkey();
                return;
            }

            if (m.Msg == WmAppConvert)
            {
                _onConvert(m.WParam);
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
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
