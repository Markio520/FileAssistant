param([ValidateSet('x64', 'x86', 'ARM64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -le 5) {
    # MSBuild can inherit PowerShell 7's module path; use the Windows PowerShell modules here.
    $env:PSModulePath = (Join-Path $PSHOME 'Modules') + ';' + (Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules')
}
if ($Architecture -ne 'x64') { throw 'The bundled search engine is currently validated for x64 only.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCache = Join-Path $taskRoot 'artifacts/dependencies/everything/1.4.1.1032/x64'
$taskArchive = Join-Path $taskCache 'Everything-1.4.1.1032.x64.zip'
$taskExecutable = Join-Path $taskCache 'Everything.exe'
$taskArchiveHash = '698DF475EC44E638F66F1B6A32D28FEA613CEC78D3B6310E6ABE53431EEB940C'
$taskExeHash = 'F191F756996A14A11E5445FA7103D302EFD510CF2FBF920E6C0C8ED51D512E36'
New-Item -ItemType Directory -Path $taskCache -Force | Out-Null
if (-not (Test-Path -LiteralPath $taskExecutable) -or (Get-FileHash -LiteralPath $taskExecutable -Algorithm SHA256).Hash -ne $taskExeHash) {
    if (-not (Test-Path -LiteralPath $taskArchive)) {
        Invoke-WebRequest 'https://www.voidtools.com/Everything-1.4.1.1032.x64.zip' -OutFile $taskArchive
    }
    if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskArchiveHash) { throw 'Everything archive checksum mismatch.' }
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskCache -Force
}
if ((Get-FileHash -LiteralPath $taskExecutable -Algorithm SHA256).Hash -ne $taskExeHash) { throw 'Everything executable checksum mismatch.' }
$taskSignature = Get-AuthenticodeSignature -LiteralPath $taskExecutable
if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'voidtools') { throw 'The official Everything signature could not be verified.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'third-party/licenses/Everything-1.4.1.1032.txt') -Destination (Join-Path $taskCache 'License.txt') -Force
