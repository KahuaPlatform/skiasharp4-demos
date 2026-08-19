[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Filter
)

# Runs the MSTest suite for all twelve arcade demos: attract soak, render smoke and repo conventions.
#
# Plain net10.0, no Uno SDK involved: the chassis and every demo's Game/ folder are
# UI-free, so they can be driven from an ordinary test host.
#
#   .\Builds\Test-Arcade.ps1
#   .\Builds\Test-Arcade.ps1 -Filter "FullyQualifiedName~AttractSoak"

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Arcade.Tests\Arcade.Tests.csproj'

Write-Host "Testing Arcade ($Configuration)..." -ForegroundColor Cyan
if ($Filter) {
    dotnet test $project -c $Configuration --nologo --filter $Filter
} else {
    dotnet test $project -c $Configuration --nologo
}
exit $LASTEXITCODE
