[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Demo,
    [string]$Out,
    [int]$DelaySeconds = 0,
    [switch]$Launch,
    [string]$Configuration = 'Release'
)

# Captures a screenshot of a running desktop demo's window.
#
# Every demo in this repo is a Skia canvas, so "did the change actually render?"
# is the question that matters most and the one a green build says nothing about.
# This is the only way to answer it without a human watching the window.
#
#   .\Builds\Capture-Demo.ps1 -Demo Eli
#   .\Builds\Capture-Demo.ps1 -Demo Eli -Launch -DelaySeconds 16   # catch attract mode
#   .\Builds\Capture-Demo.ps1 -Demo Hahai -Out shots\hahai.png
#
# Output defaults to publish/screenshots/<Demo>-<timestamp>.png. /publish/ is
# gitignored, so captures never land in the working tree by accident.
#
# Capture method: Win32 PrintWindow with PW_RENDERFULLCONTENT, which asks the
# window to render its own surface into a bitmap. That grabs the demo even when
# another window is sitting on top of it — the naive Graphics.CopyFromScreen
# approach captures the SCREEN region instead, so anything overlapping the demo
# ends up in the shot. PrintWindow can come back blank on some GPU-composited
# windows, so a blank result falls back to CopyFromScreen with a warning rather
# than silently handing back a black rectangle.
#
# Desktop only: there is no window to capture on browserwasm.

$ErrorActionPreference = 'Stop'

if ($Launch) {
    $runScript = Join-Path $PSScriptRoot "Run-$Demo.ps1"
    if (-not (Test-Path $runScript)) { throw "No run script at $runScript" }
    Write-Host "Launching $Demo ($Configuration)..." -ForegroundColor Cyan
    Start-Process -FilePath 'powershell' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $runScript, '-Configuration', $Configuration) `
        -WindowStyle Hidden | Out-Null
    # Give the build-and-start a moment before we start looking for a window.
    Start-Sleep -Seconds 8
}

if ($DelaySeconds -gt 0) {
    Write-Host "Waiting ${DelaySeconds}s before capture..." -ForegroundColor DarkGray
    Start-Sleep -Seconds $DelaySeconds
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class DemoCapture {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# The process name matches the demo folder name (Eli -> Eli.exe).
$proc = Get-Process -Name $Demo -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1

if (-not $proc) {
    Write-Host "No running '$Demo' window found." -ForegroundColor Red
    Write-Host "Start it first with .\Builds\Run-$Demo.ps1, or pass -Launch." -ForegroundColor Yellow
    exit 1
}

$rect = New-Object DemoCapture+RECT
[void][DemoCapture]::GetWindowRect($proc.MainWindowHandle, [ref]$rect)
$width  = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) { throw "Window reported a zero size ($width x $height)" }

if (-not $Out) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $stamp    = Get-Date -Format 'yyyyMMdd-HHmmss'
    $Out      = Join-Path $repoRoot ("publish\screenshots\{0}-{1}.png" -f $Demo, $stamp)
}
$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$bmp = New-Object System.Drawing.Bitmap $width, $height
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
try {
    $hdc = $gfx.GetHdc()
    try   { [void][DemoCapture]::PrintWindow($proc.MainWindowHandle, $hdc, 2) }  # PW_RENDERFULLCONTENT
    finally { $gfx.ReleaseHdc($hdc) }

    # PrintWindow returns a blank surface on some compositors. Sample a grid; if
    # every sample is identical the capture failed, so fall back to the screen grab.
    $seen = @{}
    for ($x = 4; $x -lt $width;  $x += [Math]::Max(1, [int]($width  / 12))) {
        for ($y = 4; $y -lt $height; $y += [Math]::Max(1, [int]($height / 12))) {
            $seen[$bmp.GetPixel($x, $y).ToArgb()] = $true
        }
    }
    if ($seen.Count -le 1) {
        Write-Host "PrintWindow came back blank; falling back to a screen grab." -ForegroundColor Yellow
        Write-Host "Anything overlapping the window will appear in the capture." -ForegroundColor Yellow
        $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    }

    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $gfx.Dispose()
    $bmp.Dispose()
}

Write-Host ("Captured {0} ({1}x{2}) -> {3}" -f $Demo, $width, $height, $Out) -ForegroundColor Green
if ($Launch) { Write-Host "$Demo is still running; close it when you're done." -ForegroundColor DarkGray }
exit 0
