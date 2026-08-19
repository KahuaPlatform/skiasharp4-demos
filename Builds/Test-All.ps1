[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

# Runs every test suite in the repo and prints a summary, mirroring Build-All.ps1.
#
#   Common.Tests  - the shared neon chassis (Camera2D seam maths, TileGrid wall
#                   slide, FlowField, AsciiMap, SeamlessTerrain, primitives)
#   Arcade.Tests  - all twelve arcade demos: attract soak, offscreen render smoke,
#                   and the repo conventions that build cleanly when broken
#
# Both are plain net10.0 test hosts - no Uno SDK, no window, no GPU - so this runs
# anywhere the repo builds. Deliberately NOT part of Build-All.ps1: a failing test
# should not be able to block a build.

$ErrorActionPreference = 'Stop'

$suites = @(
    'Test-Common.ps1',
    'Test-Arcade.ps1'
)

$failures = @()
foreach ($s in $suites) {
    Write-Host ""
    Write-Host "=== $s ===" -ForegroundColor Yellow
    & "$PSScriptRoot\$s" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { $failures += $s }
}

Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "All test suites passed ($Configuration)." -ForegroundColor Green
    exit 0
} else {
    Write-Host "Failed: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}
