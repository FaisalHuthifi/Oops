using System.Drawing;
using System.Windows.Forms;
using Oops.Settings;

namespace Oops.UI;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppSettings _settings;
    private readonly Action _openSettings;
    private readonly Action _exit;

    public TrayIcon(AppSettings settings, Action openSettings, Action exit)
    {
        _settings = settings;
        _openSettings = openSettings;
        _exit = exit;

        _notifyIcon = new NotifyIcon
        {
            Text = "Oops",
            Icon = SystemIcons.Application,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => _openSettings();
        RefreshMenu();
    }

    public void UpdateEnabledState(bool enabled)
    {
        _settings.Enabled = enabled;
        _notifyIcon.Text = enabled ? "Oops (Enabled)" : "Oops (Disabled)";
        RefreshMenu();
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(1500, title, message, ToolTipIcon.Info);
    }

    private void RefreshMenu()
    {
        var menu = new ContextMenuStrip();

        var header = new ToolStripLabel("Oops") { Enabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var enabledItem = new ToolStripMenuItem("Enabled")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        enabledItem.Click += (_, _) =>
        {
            _settings.Enabled = enabledItem.Checked;
            _settings.Save();
            UpdateEnabledState(_settings.Enabled);
        };
        menu.Items.Add(enabledItem);

        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => _openSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => _exit()));

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
