param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'initialize-dotnet.ps1')

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    & $taskDotnet build FileAssistant.slnx --configuration $Configuration "-p:Platform=$Architecture" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}
