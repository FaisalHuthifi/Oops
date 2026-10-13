using System.Windows;
using System.Windows.Controls;
using Oops.Models;
using Oops.Services;
using Oops.Settings;

namespace Oops.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIcon _trayIcon;

    public SettingsWindow(AppSettings settings, HotkeyService hotkeyService, TrayIcon trayIcon)
    {
        InitializeComponent();
        _settings = settings;
        _hotkeyService = hotkeyService;
        _trayIcon = trayIcon;
        LoadSettings();
    }

    private void LoadSettings()
    {
        StartupCheck.IsChecked = _settings.StartWithWindows;
        SoundCheck.IsChecked = _settings.PlaySound;
        NotificationsCheck.IsChecked = _settings.ShowNotifications;

        HotkeyCombo.SelectedIndex = _settings.HotkeyPreset switch
        {
            HotkeyPreset.CtrlShiftI => 1,
            HotkeyPreset.AltI => 2,
            _ => 0
        };
    }

    private void HotkeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HotkeyCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return;

        _settings.HotkeyPreset = tag switch
        {
            "CtrlShiftI" => HotkeyPreset.CtrlShiftI,
            "AltI" => HotkeyPreset.AltI,
            _ => HotkeyPreset.CtrlI
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartupCheck.IsChecked == true;
        _settings.PlaySound = SoundCheck.IsChecked == true;
        _settings.ShowNotifications = NotificationsCheck.IsChecked == true;
        _settings.Save();
        _settings.ApplyStartup(_settings.StartWithWindows);

        _hotkeyService.UpdateHotkey(_settings.GetHotkeyConfiguration());
        _trayIcon.UpdateEnabledState(_settings.Enabled);

        System.Windows.MessageBox.Show(this, "Settings saved.", "Oops", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
