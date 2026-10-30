$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskDirectory = Join-Path $taskRoot 'artifacts/everything-test'
$taskFixtures = Join-Path $taskDirectory 'fixtures'
$taskArchive = Join-Path $taskDirectory 'Everything-1.4.1.1032.x64.zip'
$taskExe = Join-Path $taskDirectory 'Everything.exe'
$taskConfig = Join-Path $taskDirectory 'review.ini'
$taskInstance = 'FileAssistantReview'
New-Item -ItemType Directory -Path $taskFixtures -Force | Out-Null

# The portable test instance indexes only generated fixtures, without a service or elevation.
if (Get-Process -Name Everything -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $taskExe }) {
    throw 'The isolated test instance is already running. Close that instance before running this script.'
}
if (-not (Test-Path -LiteralPath $taskArchive)) {
    Invoke-WebRequest 'https://www.voidtools.com/Everything-1.4.1.1032.x64.zip' -OutFile $taskArchive
}
$taskExpectedHash = '698df475ec44e638f66f1b6a32d28fea613cec78d3b6310e6abe53431eeb940c'
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskExpectedHash) {
    throw 'The Everything archive checksum does not match the official 1.4.1.1032 release.'
}
Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskDirectory -Force
if ((Get-AuthenticodeSignature -LiteralPath $taskExe).Status -ne 'Valid') {
    throw 'The Everything executable does not have a valid Authenticode signature.'
}
for ($taskNumber = 1; $taskNumber -le 320; $taskNumber++) {
    [IO.File]::WriteAllText((Join-Path $taskFixtures ('交付清单_{0:000}.txt' -f $taskNumber)), ('测试内容 ' * $taskNumber), [Text.UTF8Encoding]::new($false))
}
[IO.File]::WriteAllBytes((Join-Path $taskFixtures '产品 主图_最终确认版.png'), [byte[]]::new(8192))
[IO.File]::WriteAllText((Join-Path $taskFixtures '报价.pdf'), 'metadata fixture')
[IO.File]::WriteAllText((Join-Path $taskFixtures '备份.txt'), 'metadata fixture')
foreach ($taskExtension in @('mp3', 'zip', 'exe', 'msi', 'com', 'bat', 'cmd', 'mp4')) {
    # Metadata only: these generated fixtures are never opened or executed.
    [IO.File]::WriteAllText((Join-Path $taskFixtures ('类型样本.' + $taskExtension)), 'metadata fixture')
}
New-Item -ItemType Directory -Path (Join-Path $taskFixtures '类型文件夹.exe') -Force | Out-Null
# INI quoted list values escape backslashes, unlike ordinary Windows paths.
$taskLines = @(
    '[Everything]', 'app_data=0', 'run_as_admin=0', 'run_in_background=1',
    'show_tray_icon=0', 'check_for_updates_on_startup=0',
    'auto_include_fixed_volumes=0', 'auto_include_removable_volumes=0',
    'ntfs_volume_guids=', 'ntfs_volume_paths=', 'ntfs_volume_includes=',
    ('folders="{0}"' -f $taskFixtures.Replace('\', '\\')),
    'folder_monitor_changes=1', 'folder_update_types=0',
    'index_size=1', 'index_date_modified=1', 'fast_size_sort=1', 'fast_date_modified_sort=1',
    'http_server_enabled=0', 'etp_server_enabled=0'
)
[IO.File]::WriteAllLines($taskConfig, $taskLines, [Text.UTF8Encoding]::new($false))
$taskProcess = $null
$taskPreviousEngine = $env:FILEASSISTANT_TEST_ENGINE
try {
    $env:FILEASSISTANT_TEST_ENGINE = $taskExe
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @('-instance', $taskInstance, '-config', ('"{0}"' -f $taskConfig), '-startup', '-reindex') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    & (Join-Path $PSScriptRoot 'test.ps1') -Everything
} finally {
    $env:FILEASSISTANT_TEST_ENGINE = $taskPreviousEngine
    if ($taskProcess -and -not $taskProcess.HasExited) {
        Start-Process -FilePath $taskExe -ArgumentList @('-instance', $taskInstance, '-exit') -WindowStyle Hidden -Wait
        if (-not $taskProcess.WaitForExit(5000)) { throw 'The isolated Everything test instance did not exit.' }
    }
}
