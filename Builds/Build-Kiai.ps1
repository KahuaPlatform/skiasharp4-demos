[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Wasm
)

$ErrorActionPreference = 'Stop'
$framework = if ($Wasm) { 'net10.0-browserwasm' } else { 'net10.0-desktop' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Kiai\Kiai\Kiai.csproj'

Write-Host "Building Kiai ($Configuration / $framework)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -f $framework --nologo
exit $LASTEXITCODE
