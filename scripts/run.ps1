param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Architecture = 'x64',
    [switch]$Unpackaged
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'initialize-dotnet.ps1')

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $taskArguments = @('run', '--project', 'src/FileAssistant.App/FileAssistant.App.csproj', '--no-launch-profile', '--configuration', $Configuration, "-p:Platform=$Architecture")
    if ($Unpackaged) { $taskArguments += @('-p:WindowsPackageType=None', '-p:WindowsAppSDKSelfContained=true') }
    & $taskDotnet @taskArguments
    if ($LASTEXITCODE -ne 0) { throw "Application launch failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}
