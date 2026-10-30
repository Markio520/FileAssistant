param([switch]$Everything)
$ErrorActionPreference = 'Stop'
$taskDotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$taskDotnet = if ($taskDotnetCommand) { $taskDotnetCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe' }
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $taskFilter = if ($Everything) { 'Category=Everything' } else { 'Category!=Everything' }
    $taskProjects = if ($Everything) { @('tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj') } else { @('tests/FileAssistant.Core.Tests/FileAssistant.Core.Tests.csproj', 'tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj', 'tests/FileAssistant.App.Tests/FileAssistant.App.Tests.csproj') }
    foreach ($taskProject in $taskProjects) {
        & $taskDotnet test $taskProject --filter $taskFilter --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $taskProject" }
    }
} finally { Pop-Location }
