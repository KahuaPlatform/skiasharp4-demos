[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Wasm
)

$ErrorActionPreference = 'Stop'
$framework = if ($Wasm) { 'net10.0-browserwasm' } else { 'net10.0-desktop' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Kanapi\Kanapi\Kanapi.csproj'

Write-Host "Building Kanapi ($Configuration / $framework)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -f $framework --nologo
exit $LASTEXITCODE
