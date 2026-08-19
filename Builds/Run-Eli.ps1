[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Wasm
)

$ErrorActionPreference = 'Stop'
$framework = if ($Wasm) { 'net10.0-browserwasm' } else { 'net10.0-desktop' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Eli\Eli\Eli.csproj'

Write-Host "Running Eli ($Configuration / $framework)..." -ForegroundColor Cyan
dotnet run --project $project -c $Configuration -f $framework
exit $LASTEXITCODE
