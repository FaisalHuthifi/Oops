namespace Oops.Models;

public enum HotkeyPreset
{
    CtrlI,
    CtrlShiftI,
    AltI
}

public sealed class HotkeyConfiguration
{
    public HotkeyPreset Preset { get; set; } = HotkeyPreset.CtrlI;
    public uint Modifiers { get; set; } = NativeConstants.MOD_CONTROL;
    public uint VirtualKey { get; set; } = 0x49; // I
}

public sealed class KeyboardMapData
{
    public Dictionary<string, string> ArabicToEnglish { get; set; } = new();
    public Dictionary<string, string> EnglishToArabic { get; set; } = new();
}
