param([ValidateSet('x64','x86','ARM64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -le 5) {
    $env:PSModulePath = (Join-Path $PSHOME 'Modules') + ';' + (Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules')
}
if ($Architecture -ne 'x64') { throw 'Local processing engines are currently verified only for Windows x64.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskManifestPath = Join-Path $taskRoot 'engines/manifest.json'
$taskEngine = (Get-Content -LiteralPath $taskManifestPath -Encoding UTF8 -Raw | ConvertFrom-Json).ffmpeg
$taskCache = Join-Path $taskRoot "artifacts/dependencies/ffmpeg/$($taskEngine.version)/x64"
$taskArchive = Join-Path $taskCache $taskEngine.archive
$taskReady = Join-Path $taskCache 'bundle/bin/ffmpeg.exe'
New-Item -ItemType Directory -Path $taskCache -Force | Out-Null
if (-not (Test-Path -LiteralPath $taskArchive)) {
    $taskPart = $taskArchive + '.part'
    Invoke-WebRequest -Uri $taskEngine.url -OutFile $taskPart
    if ((Get-FileHash -LiteralPath $taskPart -Algorithm SHA256).Hash -ne $taskEngine.sha256) { throw 'FFmpeg archive checksum mismatch.' }
    Move-Item -LiteralPath $taskPart -Destination $taskArchive
}
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskEngine.sha256) { throw 'FFmpeg archive checksum mismatch.' }
if (-not (Test-Path -LiteralPath $taskReady)) {
    $taskExtraction = Join-Path $taskCache 'extracted'
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskExtraction -Force
    $taskRoots = @(Get-ChildItem -LiteralPath $taskExtraction -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/ffmpeg.exe') })
    if ($taskRoots.Count -ne 1) { throw 'Unexpected FFmpeg archive layout.' }
    Copy-Item -LiteralPath $taskRoots[0].FullName -Destination (Join-Path $taskCache 'bundle') -Recurse -Force
}
$taskBuildInfo = & $taskReady -hide_banner -buildconf 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $taskBuildInfo -match '--enable-gpl|--enable-nonfree') { throw 'Unexpected FFmpeg build or license configuration.' }
Copy-Item -LiteralPath $taskManifestPath -Destination (Join-Path $taskCache 'bundle/manifest.json') -Force
