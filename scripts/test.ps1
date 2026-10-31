param([switch]$Everything)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'initialize-dotnet.ps1')
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    if (-not $Everything) { & (Join-Path $PSScriptRoot 'restore-processing-engines.ps1') }
    $taskFilter = if ($Everything) { 'Category=Everything' } else { 'Category!=Everything' }
    $taskProjects = if ($Everything) { @('tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj') } else { @('tests/FileAssistant.Core.Tests/FileAssistant.Core.Tests.csproj', 'tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj', 'tests/FileAssistant.App.Tests/FileAssistant.App.Tests.csproj') }
    foreach ($taskProject in $taskProjects) {
        & $taskDotnet test $taskProject --filter $taskFilter --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $taskProject" }
    }
} finally { Pop-Location }
