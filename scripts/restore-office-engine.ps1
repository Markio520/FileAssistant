param([switch]$Extract)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskPackage = (Get-Content -LiteralPath (Join-Path $taskRepo 'engines/manifest.json') -Raw | ConvertFrom-Json).libreoffice
$taskCache = Join-Path $taskRepo ('artifacts/dependencies/libreoffice/' + $taskPackage.version)
New-Item -ItemType Directory -Force -Path $taskCache | Out-Null
$taskArchive = Join-Path $taskCache $taskPackage.archive
if (-not (Test-Path -LiteralPath $taskArchive) -or (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskPackage.sha256) {
    & curl.exe --fail --location --retry 2 --output $taskArchive $taskPackage.url
    if ($LASTEXITCODE -ne 0) { throw 'LibreOffice download failed.' }
}
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskPackage.sha256) { throw 'LibreOffice checksum mismatch.' }
if ($Extract) {
    $taskExtracted = Join-Path $taskCache 'extracted'
    if (-not (Test-Path -LiteralPath (Join-Path $taskExtracted 'program/soffice.com'))) {
        $taskProcess = Start-Process -FilePath (Join-Path $env:WINDIR 'System32/msiexec.exe') -ArgumentList "/a `"$taskArchive`" /qn /norestart TARGETDIR=`"$taskExtracted`"" -WindowStyle Hidden -Wait -PassThru
        if ($taskProcess.ExitCode -ne 0) { throw "LibreOffice extraction failed: $($taskProcess.ExitCode)" }
    }
}
Write-Output "Verified LibreOffice $($taskPackage.version) developer cache."
