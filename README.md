# Oops

A lightweight Windows tray app that fixes text typed with the wrong keyboard layout (Arabic ↔ English).

## How it works

1. Type text with the wrong keyboard layout
2. Select the text
3. Press **Ctrl + I**
4. The selected text is instantly converted and replaced in place (Oops never uses the Windows clipboard)

Works in any editable text surface — Notepad, Office, browsers, chat and Electron apps,
search boxes and address bars — using three replacement strategies in order: native Win32
edit messages, Unicode keystrokes over the live selection, and UI Automation.

## Examples

| Typed (wrong layout) | After Ctrl + I |
|----------------------|----------------|
| `اثممخ`              | `hello`        |
| `lvpfh`              | `مرحبا`        |

Conversion maps keyboard **positions**, not language meaning. No internet or AI required.

## Download

Download **Oops.exe** (or the zip) from the [latest release](https://github.com/FaisalHuthifi/Oops/releases/latest).

1. Download `Oops.exe`
2. If Windows blocks it: right-click the file → **Properties** → check **Unblock** → OK
3. Run it — a notification appears and the tray icon shows near the clock (click the ^ arrow if hidden)
4. Right-click the tray icon for **Settings** or **Exit**

No installer required. Windows may show SmartScreen on first run — click **More info** → **Run anyway**.

**Requirements:** Windows 10/11 (64-bit)

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```powershell
git clone https://github.com/FaisalHuthifi/Oops.git
cd Oops
dotnet run --project src/Oops.csproj
```

Publish a standalone executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `src\bin\Release\net8.0-windows\win-x64\publish\Oops.exe`

## Settings

- **Hotkey** — Ctrl + I (default), Ctrl + Shift + I, or Alt + I
- **Start with Windows**
- **Play sound** after conversion
- **Show notification** when text is converted

Settings are stored in `%AppData%\Oops\settings.json`.

## Performance

- Memory: ~65–110 MB (WPF + .NET 8 runtime)
- Idle CPU: near zero
- Conversion: under 100 ms in Notepad; up to ~200 ms in some modern apps
- Download size: ~146 MB (self-contained, no .NET install needed)

## License

MIT
