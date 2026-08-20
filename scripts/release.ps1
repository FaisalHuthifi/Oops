# Publish Oops to GitHub (run after: gh auth login)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$version = "v1.0.5"
$publishDir = Join-Path $root "publish"
$exe = Join-Path $publishDir "Oops.exe"
$zip = Join-Path $publishDir "Oops-win-x64.zip"
$howto = Join-Path $publishDir "HOWTO.txt"

Write-Host "Building release..."
Push-Location $root
dotnet publish Oops.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
Pop-Location

if (-not (Test-Path $exe)) {
    throw "Build failed: $exe not found"
}

@'
Oops - Quick start
==================

1. Run Oops.exe
2. If nothing seems to happen:
   - Right-click Oops.exe -> Properties -> check "Unblock" -> OK, then run again
   - If SmartScreen appears: More info -> Run anyway
   - Look for the tray icon near the clock (click the ^ arrow to show hidden icons)
3. Select mistyped text and press Ctrl+I

Requirements: Windows 10/11 (64-bit). No .NET install needed.
'@ | Set-Content -Path $howto -Encoding UTF8

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $exe, $howto -DestinationPath $zip

$repo = $null
try {
    $repo = gh repo view --json nameWithOwner -q .nameWithOwner 2>$null
} catch {
    $repo = $null
}

if (-not $repo) {
    Write-Host "Creating GitHub repository..."
    gh repo create Oops --public --source=$root --remote=origin --push
} else {
    Write-Host "Pushing to $repo..."
    Push-Location $root
    git push -u origin master
    Pop-Location
}

gh release create $version $exe $zip `
    --repo $repo `
    --title "Oops $version" `
    --notes @"
## Oops $version

Fix text typed with the wrong keyboard layout (Arabic <-> English).

### Fixes in v1.0.5
- **The clipboard is never touched.** All copy/paste code was removed, so your clipboard and Win+V clipboard history stay exactly as you left them
- **Works in any editable text field** — browsers, Office, chat and Electron apps, address bars and search boxes — not just Notepad
- Replacement is verified before reporting success, so you no longer get a "Text converted" notification when nothing changed
- Read-only fields are detected and skipped instead of receiving stray text
- Support for more native rich text controls

### Fixes in v1.0.4
- Fix Ctrl+I not firing (use WinForms message window for hotkey delivery)
- Improve replace reliability across apps

### Fixes in v1.0.3
- Fix crash on startup in downloaded single-file exe (replace WPF hidden window with native Win32)
- Works when run from OneDrive or folders with non-English names

### How to use
1. Download **Oops.exe** or **Oops-win-x64.zip** below
2. If Windows blocks the download: right-click -> **Properties** -> **Unblock**
3. Run it — look for the tray icon near the clock (^ arrow for hidden icons)
4. Select mistyped text and press **Ctrl + I**

### Requirements
- Windows 10/11 (64-bit)
- No .NET install needed (self-contained)

### Notes
Windows SmartScreen may warn on first run (unsigned app) — click **More info** -> **Run anyway**.
"@

if (-not $repo) {
    $repo = gh repo view --json nameWithOwner -q .nameWithOwner
}
Write-Host "Done! Release: https://github.com/$repo/releases/tag/$version"
