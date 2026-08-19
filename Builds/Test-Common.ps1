[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Filter
)

# Runs the MSTest suite for the shared neon chassis in Source/Common/.
#
# Plain net10.0, no Uno SDK involved: the chassis and every demo's Game/ folder are
# UI-free, so they can be driven from an ordinary test host.
#
#   .\Builds\Test-Common.ps1
#   .\Builds\Test-Common.ps1 -Filter "FullyQualifiedName~Camera2DTests"

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'Source\Common.Tests\Common.Tests.csproj'

Write-Host "Testing Common ($Configuration)..." -ForegroundColor Cyan
if ($Filter) {
    dotnet test $project -c $Configuration --nologo --filter $Filter
} else {
    dotnet test $project -c $Configuration --nologo
}
exit $LASTEXITCODE
