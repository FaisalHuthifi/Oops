$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$oopsExe = Join-Path $root "src\bin\Release\net8.0-windows\Oops.exe"
$inputText = [char]0x0627 + [char]0x062B + [char]0x0645 + [char]0x0645 + [char]0x062E
$expected = "hello"

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class TestWin32
{
    public const int WM_SETTEXT = 0x000C;
    public const int EM_SETSEL = 0x00B1;
    public const int EM_GETTEXTLENGTH = 0x000E;
    public const int EM_GETTEXT = 0x000D;

    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] public static extern IntPtr SendMessageText(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);

    public static IntPtr FindNotepadEdit()
    {
        IntPtr notepad = IntPtr.Zero;
        IntPtr edit = IntPtr.Zero;

        EnumWindows((h, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() == "Notepad")
            {
                notepad = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (notepad == IntPtr.Zero) return IntPtr.Zero;

        EnumChildWindows(notepad, (h, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            var cls = sb.ToString();
            if (cls == "Edit" || cls.Contains("RichEdit"))
            {
                edit = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return edit;
    }

    public static string ReadEditText(IntPtr edit)
    {
        var len = (int)SendMessage(edit, EM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
        if (len <= 0) return string.Empty;
        var buf = new StringBuilder(len + 1);
        SendMessageText(edit, EM_GETTEXT, (IntPtr)(len + 1), buf);
        return buf.ToString();
    }
}
"@

Add-Type -AssemblyName System.Windows.Forms

function Stop-TestProcesses {
    Stop-Process -Name Oops,notepad -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

Write-Host "=== Oops conversion test ===" -ForegroundColor Cyan

if (-not (Test-Path $oopsExe)) {
    throw "Build not found: $oopsExe - run: dotnet build -c Release"
}

Stop-TestProcesses

$oops = Start-Process $oopsExe -PassThru
Start-Sleep -Seconds 2
if ($oops.HasExited) { throw "Oops exited with code $($oops.ExitCode)" }

Start-Process notepad | Out-Null
Start-Sleep -Seconds 2

$edit = [TestWin32]::FindNotepadEdit()
if ($edit -eq [IntPtr]::Zero) {
    Stop-TestProcesses
    throw "Could not find Notepad edit control."
}

[void][TestWin32]::SendMessage($edit, [TestWin32]::WM_SETTEXT, [IntPtr]::Zero, $inputText)
[void][TestWin32]::SendMessage($edit, [TestWin32]::EM_SETSEL, [IntPtr]::Zero, [IntPtr]::new(-1))

$notepadWindow = [IntPtr]::Zero
[TestWin32]::EnumWindows({
    param($h, $_)
    $sb = New-Object System.Text.StringBuilder 256
    [void][TestWin32]::GetClassName($h, $sb, 256)
    if ($sb.ToString() -eq "Notepad") { $script:notepadWindow = $h; return $false }
    return $true
}, [IntPtr]::Zero)

[void][TestWin32]::SetForegroundWindow($notepadWindow)
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("^i")
Start-Sleep -Milliseconds 800

$actualText = [TestWin32]::ReadEditText($edit)
$conversionOk = ($actualText -eq $expected)

Write-Host "Expected:   $expected"
Write-Host "Actual:     $actualText"
Write-Host "Conversion: $(if ($conversionOk) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($conversionOk) { 'Green' } else { 'Red' })

Stop-TestProcesses

if (-not $conversionOk) { exit 1 }
Write-Host "`nTest passed." -ForegroundColor Green
