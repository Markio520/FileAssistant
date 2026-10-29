param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'
$taskDotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$taskDotnet = if ($taskDotnetCommand) { $taskDotnetCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $taskDotnet)) { throw 'Install the .NET SDK specified in global.json first.' }
$env:DOTNET_ROOT = Split-Path -Parent $taskDotnet
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    & $taskDotnet build FileAssistant.slnx --configuration $Configuration "-p:Platform=$Architecture" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}
