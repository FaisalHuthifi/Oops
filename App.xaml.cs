using System.Windows;
using System.Windows.Forms;
using Oops.Services;
using Oops.Settings;
using Oops.UI;
using Application = System.Windows.Application;

namespace Oops;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\Oops_SingleInstance_v1";

    private static Mutex? _singleInstanceMutex;
    private MessageWindow? _messageWindow;
    private TrayIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private ReplaceService? _replaceService;
    private AppSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Oops is already running. Check the system tray near the clock.",
                "Oops",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settings = AppSettings.Load();
        _settings.ApplyStartup(_settings.StartWithWindows);

        _messageWindow = new MessageWindow();
        _messageWindow.Show();

        var source = _messageWindow.CreateSource();

        var clipboard = new ClipboardService();
        var selection = new SelectionService(clipboard, _messageWindow.Handle);
        _replaceService = new ReplaceService(
            clipboard,
            selection,
            new TextConverter(),
            _settings);

        _hotkeyService = new HotkeyService(source, _settings.GetHotkeyConfiguration());
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        _trayIcon = new TrayIcon(_settings, ShowSettings, ExitApplication);
        _trayIcon.UpdateEnabledState(_settings.Enabled);
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        _replaceService?.PrepareTargetWindow(e.TargetWindow);
        var result = _replaceService?.TryConvertSelection();
        ShowResultNotification(result);
    }

    private void ShowResultNotification(ReplaceResult? result)
    {
        if (_settings?.ShowNotifications != true)
            return;

        switch (result)
        {
            case ReplaceResult.Success:
                _trayIcon?.ShowBalloon("Oops", "Text converted.");
                break;
            case ReplaceResult.NoSelection:
                _trayIcon?.ShowBalloon("Oops", "No text selected.");
                break;
            case ReplaceResult.ClipboardFailed:
                _trayIcon?.ShowBalloon("Oops", "Could not access clipboard.");
                break;
            case ReplaceResult.Unchanged:
                _trayIcon?.ShowBalloon("Oops", "Text does not need conversion.");
                break;
            case ReplaceResult.Failed:
                _trayIcon?.ShowBalloon("Oops", "Conversion failed.");
                break;
        }
    }

    private void ShowSettings()
    {
        var existing = Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            existing.Focus();
            return;
        }

        var window = new SettingsWindow(_settings!, _hotkeyService!, _trayIcon!);
        window.Show();
        window.Activate();
    }

    private void ExitApplication()
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _messageWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
