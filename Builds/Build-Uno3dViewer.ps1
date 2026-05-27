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

Write-Host "Building Uno3dViewer ($Configuration / net10.0-desktop)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -f net10.0-desktop --nologo
exit $LASTEXITCODE
