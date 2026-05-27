[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Wasm
)

$ErrorActionPreference = 'Stop'

if ($Wasm) {
    Write-Host "Uno3dViewer is desktop-only (net10.0-desktop). No wasm TFM available." -ForegroundColor Red
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Uno3dViewer\Uno3dViewer\Uno3dViewer.csproj'

Write-Host "Running Uno3dViewer ($Configuration / net10.0-desktop)..." -ForegroundColor Cyan
dotnet run --project $project -c $Configuration -f net10.0-desktop
exit $LASTEXITCODE
