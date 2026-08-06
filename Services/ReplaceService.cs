using System.Media;
using Oops.Settings;

namespace Oops.Services;

public sealed class ReplaceService
{
    private readonly SelectionService _selection;
    private readonly ClipboardService _clipboard;
    private readonly TextConverter _converter;
    private readonly AppSettings _settings;

    private IntPtr _targetWindow;

    public ReplaceService(
        ClipboardService clipboard,
        SelectionService selection,
        TextConverter converter,
        AppSettings settings)
    {
        _clipboard = clipboard;
        _selection = selection;
        _converter = converter;
        _settings = settings;
    }

    public void PrepareTargetWindow(IntPtr targetWindow) =>
        _targetWindow = _selection.ResolveTargetWindow(targetWindow);

    public ReplaceResult TryConvertSelection()
    {
        if (!_settings.Enabled)
            return ReplaceResult.Disabled;

        try
        {
            var selection = _selection.TryGetSelection(_targetWindow);
            if (selection == null || string.IsNullOrWhiteSpace(selection.Text))
                return ReplaceResult.NoSelection;

            var converted = _converter.Convert(selection.Text);
            if (converted == selection.Text)
                return ReplaceResult.Unchanged;

            if (!selection.TryReplace(converted, _clipboard))
                return ReplaceResult.ClipboardFailed;

            if (_settings.PlaySound)
                SystemSounds.Asterisk.Play();

            return ReplaceResult.Success;
        }
        catch
        {
            return ReplaceResult.Failed;
        }
    }
}

public enum ReplaceResult
{
    Success,
    NoSelection,
    Unchanged,
    Disabled,
    ClipboardFailed,
    Failed
}
