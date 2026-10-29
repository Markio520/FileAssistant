$ErrorActionPreference = 'Stop'
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
$taskPackageVersions = [xml](Get-Content -LiteralPath (Join-Path $taskProjectRoot 'Directory.Packages.props') -Raw)

$taskDotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$taskDotnet = if ($taskDotnetCommand) { $taskDotnetCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $taskDotnet)) { throw 'The .NET SDK is missing. See docs/development-environment.md.' }
$env:DOTNET_ROOT = Split-Path -Parent $taskDotnet
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH

$taskWinAppVersion = $taskPackageVersions.SelectSingleNode("/Project/ItemGroup/PackageVersion[@Include='Microsoft.Windows.SDK.BuildTools.WinApp']").Version
$taskWinAppCommand = Get-Command winapp -ErrorAction SilentlyContinue
$taskWinApp = if ($taskWinAppCommand) { $taskWinAppCommand.Source } else { Join-Path $env:LOCALAPPDATA "Microsoft/WinAppCLI/$taskWinAppVersion/winapp.exe" }
if (-not (Test-Path -LiteralPath $taskWinApp)) { throw 'The standalone WinApp CLI is missing. See docs/development-environment.md.' }

Push-Location $taskProjectRoot
try {
    $taskSdkVersion = & $taskDotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'The SDK required by global.json is unavailable.' }
    Write-Output ".NET SDK: $taskSdkVersion"
    & $taskWinApp --version
    if ($LASTEXITCODE -ne 0) { throw 'WinApp CLI could not start.' }

    $taskCacheOutput = & $taskDotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the NuGet package cache.' }
    $taskNuGetRoot = ($taskCacheOutput -split ':', 2)[1].Trim()
    $taskBuildToolsVersion = $taskPackageVersions.SelectSingleNode("/Project/ItemGroup/PackageVersion[@Include='Microsoft.Windows.SDK.BuildTools']").Version
    $taskBuildToolsRoot = Join-Path $taskNuGetRoot "microsoft.windows.sdk.buildtools/$taskBuildToolsVersion"
    foreach ($taskToolName in @('makeappx.exe', 'makepri.exe', 'signtool.exe')) {
        $taskMatches = @(Get-ChildItem -LiteralPath $taskBuildToolsRoot -Filter $taskToolName -File -Recurse | Where-Object { $_.Directory.Name -eq 'x64' })
        if ($taskMatches.Count -ne 1) { throw "Expected one x64 $taskToolName in the configured Windows SDK build tools package." }
        Write-Output "${taskToolName}: $($taskMatches[0].FullName)"
    }

    $taskDeveloperMode = Get-ItemProperty -LiteralPath 'HKLM:/SOFTWARE/Microsoft/Windows/CurrentVersion/AppModelUnlock' -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue
    Write-Output "Windows Developer Mode enabled: $($taskDeveloperMode.AllowDevelopmentWithoutDevLicense -eq 1)"
    Write-Output 'SDK and build tools are available. Developer Mode is only required for packaged development deployment.'
} finally {
    Pop-Location
}
