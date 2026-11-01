param()
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskExecutable = Join-Path $taskRepo 'src/FileAssistant.App/bin/unpackaged/x64/Debug/net10.0-windows10.0.26100.0/win-x64/FileAssistant.App.exe'
if (Get-Process -Name FileAssistant.App -ErrorAction SilentlyContinue) {
    throw '请先正常关闭当前 FileAssistant，让软件保存列表与分组，然后重新运行本脚本。不会强制结束进程或同时启动两个实例。'
}
if (-not (Test-Path -LiteralPath $taskExecutable)) { throw '此入口对应本机阶段构建；请用 scripts/run.ps1 -Unpackaged 重新构建运行。' }
# The user invokes this script to open the interactive application.
Start-Process -FilePath $taskExecutable -WorkingDirectory (Split-Path -Parent $taskExecutable)
