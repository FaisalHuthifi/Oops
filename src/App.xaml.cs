using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Oops.Services;
using Oops.Settings;
using Oops.UI;
using Application = System.Windows.Application;

namespace Oops;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\Oops_SingleInstance_v1";

    private static Mutex? _singleInstanceMutex;
    private TrayIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private ReplaceService? _replaceService;
    private AppSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ShowStartupError(args.Exception.Message);
            args.Handled = true;
            Shutdown();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                ShowStartupError(ex.Message);
        };

        try
        {
            StartApplication(e);
        }
        catch (Exception ex)
        {
            ShowStartupError(ex.Message);
            Shutdown();
        }
    }

    private static void ShowStartupError(string message)
    {
        try
        {
            System.Windows.MessageBox.Show(
                $"Oops failed to start:\n\n{message}",
                "Oops",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            System.Windows.Forms.MessageBox.Show(
                $"Oops failed to start:\n\n{message}",
                "Oops",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void StartApplication(StartupEventArgs e)
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

        _trayIcon = new TrayIcon(_settings, ShowSettings, ExitApplication);
        _trayIcon.UpdateEnabledState(_settings.Enabled);

        _hotkeyService = new HotkeyService(_settings.GetHotkeyConfiguration());
        if (!_hotkeyService.IsRegistered)
        {
            _trayIcon.ShowBalloon(
                "Oops",
                "Could not register Ctrl+I hotkey. It may be in use by another app.");
            return;
        }

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        var selection = new SelectionService(_hotkeyService.WindowHandle);
        _replaceService = new ReplaceService(
            selection,
            new TextConverter(),
            _settings);

        _trayIcon.ShowBalloon(
            "Oops is running",
            "Look for the icon in the system tray. Select text and press Ctrl+I.");
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
            case ReplaceResult.ReplaceFailed:
                _trayIcon?.ShowBalloon("Oops", "Could not replace selected text.");
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
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
