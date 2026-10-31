param([switch]$Everything, [switch]$Office)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'initialize-dotnet.ps1')
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    if (-not $Everything) { & (Join-Path $PSScriptRoot 'restore-processing-engines.ps1') }
    if ($Office) { & (Join-Path $PSScriptRoot 'restore-office-engine.ps1') -Extract }
    $taskFilter = if ($Everything) { 'Category=Everything' } elseif ($Office) { 'Category=Office' } else { 'Category!=Everything&Category!=Office' }
    $taskProjects = if ($Everything -or $Office) { @('tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj') } else { @('tests/FileAssistant.Core.Tests/FileAssistant.Core.Tests.csproj', 'tests/FileAssistant.Infrastructure.Tests/FileAssistant.Infrastructure.Tests.csproj', 'tests/FileAssistant.App.Tests/FileAssistant.App.Tests.csproj') }
    foreach ($taskProject in $taskProjects) {
        & $taskDotnet test $taskProject --filter $taskFilter --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $taskProject" }
    }
} finally { Pop-Location }
