$taskDotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$taskDotnetCandidates = @(
    $(if ($taskDotnetCommand) { $taskDotnetCommand.Source })
    (Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique
$taskDotnet = $null

# A runtime-only system installation must not hide the user-installed SDK.
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    foreach ($taskCandidate in $taskDotnetCandidates) {
        try {
            $taskSdkVersion = & $taskCandidate --version 2>$null
            if ($LASTEXITCODE -eq 0) { $taskDotnet = $taskCandidate; break }
        } catch {
            # Try the documented user installation when this host cannot resolve global.json.
        }
    }
} finally { Pop-Location }

if (-not $taskDotnet) { throw 'Install the .NET SDK specified in global.json first. See docs/development-environment.md.' }
$env:DOTNET_ROOT = Split-Path -Parent $taskDotnet
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
