# Publish Oops to GitHub (run after: gh auth login)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$version = "v1.0.0"
$publishDir = Join-Path $root "publish"
$exe = Join-Path $publishDir "Oops.exe"

Write-Host "Building release..."
Push-Location $root
dotnet publish Oops.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
Pop-Location

if (-not (Test-Path $exe)) {
    throw "Build failed: $exe not found"
}

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

gh release create $version $exe `
    --repo $repo `
    --title "Oops $version" `
    --notes @"
## Oops $version

Fix text typed with the wrong keyboard layout (Arabic <-> English).

### How to use
1. Download **Oops.exe** below
2. Run it — look for the tray icon near the clock
3. Select mistyped text and press **Ctrl + I**

### Requirements
- Windows 10/11 (64-bit)
- No .NET install needed (self-contained)

### Notes
Windows SmartScreen may warn on first run (unsigned app) — click **More info** → **Run anyway**.
"@

if (-not $repo) {
    $repo = gh repo view --json nameWithOwner -q .nameWithOwner
}
Write-Host "Done! Release: https://github.com/$repo/releases/tag/$version"
