using System.IO;
using System.Text.Json;
using Oops.Models;

namespace Oops.Settings;

public sealed class AppSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Oops");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool PlaySound { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public HotkeyPreset HotkeyPreset { get; set; } = HotkeyPreset.CtrlI;

    public HotkeyConfiguration GetHotkeyConfiguration() => HotkeyPreset switch
    {
        HotkeyPreset.CtrlShiftI => new HotkeyConfiguration
        {
            Preset = HotkeyPreset.CtrlShiftI,
            Modifiers = NativeConstants.MOD_CONTROL | NativeConstants.MOD_SHIFT,
            VirtualKey = 0x49
        },
        HotkeyPreset.AltI => new HotkeyConfiguration
        {
            Preset = HotkeyPreset.AltI,
            Modifiers = NativeConstants.MOD_ALT,
            VirtualKey = 0x49
        },
        _ => new HotkeyConfiguration
        {
            Preset = HotkeyPreset.CtrlI,
            Modifiers = NativeConstants.MOD_CONTROL,
            VirtualKey = 0x49
        }
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public void ApplyStartup(bool enabled)
    {
        const string appName = "Oops";
        var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

        if (runKey is null)
            return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                runKey.SetValue(appName, $"\"{exePath}\"");
        }
        else
        {
            runKey.DeleteValue(appName, throwOnMissingValue: false);
        }

        runKey.Close();
    }
}
