[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Wasm
)

$ErrorActionPreference = 'Stop'

$scripts = @(
    'Build-UnoGallery.ps1',
    'Build-Pohaku.ps1',
    'Build-KahuaNetwork.ps1',
    'Build-HokuLele.ps1',
    'Build-Uno3dViewer.ps1'
)

$failures = @()
$skipped  = @()
foreach ($s in $scripts) {
    if ($Wasm -and $s -eq 'Build-Uno3dViewer.ps1') {
        Write-Host ""
        Write-Host "=== $s (skipped — desktop-only) ===" -ForegroundColor DarkYellow
        $skipped += $s
        continue
    }

    Write-Host ""
    Write-Host "=== $s ===" -ForegroundColor Yellow
    if ($Wasm) {
        & "$PSScriptRoot\$s" -Configuration $Configuration -Wasm
    } else {
        & "$PSScriptRoot\$s" -Configuration $Configuration
    }
    if ($LASTEXITCODE -ne 0) { $failures += $s }
}

$target = if ($Wasm) { 'wasm' } else { 'desktop' }
Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "All builds succeeded ($Configuration / $target)." -ForegroundColor Green
    if ($skipped.Count -gt 0) { Write-Host "Skipped: $($skipped -join ', ')" -ForegroundColor DarkYellow }
    exit 0
} else {
    Write-Host "Failed: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}
