# 开发环境与下载记录

检查日期：2026-10-29。当前使用 Windows 11 x64 上的 .NET / WinUI 命令行开发环境。

## 当前所需环境

| 环境或组件 | 版本 | 用途与位置 |
| --- | --- | --- |
| .NET SDK | 10.0.403 | C# 编译器、MSBuild、NuGet CLI；位于 `%LOCALAPPDATA%\Microsoft\dotnet` |
| .NET 与桌面运行时 | 10.0.14 | 随 SDK 安装，支持运行 .NET 桌面程序 |
| WinApp CLI | 0.7.1 | 独立命令行开发、调试、界面检查与打包工具；位于 `%LOCALAPPDATA%\Microsoft\WinAppCLI\0.7.1` |
| WinUI C# 模板 | 0.0.7-alpha | 已安装，提供 `winui-mvvm` 等官方项目模板 |
| Windows App SDK | 2.6.4 | 已通过 NuGet 还原；工程版本由 `Directory.Packages.props` 管理 |
| Windows App Runtime | 2.6.4.0 | 本机已存在 x64、x86 版本 |
| Windows SDK BuildTools | 10.0.28000.2957 | 包含 MakeAppx、MakePRI、SignTool，已验证 x64 工具能启动 |
| CommunityToolkit.Mvvm | 8.4.2 | 已通过 NuGet 还原，提供属性通知与命令生成 |
| 发布所需依赖 | 由锁文件记录 | Release 构建和 MSIX 打包已触发还原，缓存包含相应发布工具依赖 |

项目 NuGet 依赖缓存在 `%USERPROFILE%\.nuget\packages`；若设置了自定义缓存位置，以 `dotnet nuget locals global-packages --list` 的输出为准。

SDK 与独立 CLI 目录加入当前用户的 PATH。已有终端及编辑器可能仍持有旧环境变量，重新打开后可使用 `dotnet` 和 `winapp`；项目脚本也能从上述用户目录查找工具。

## 下载来源与校验

- [.NET 官方下载](https://dotnet.microsoft.com/download/dotnet/10.0)：SDK 压缩包依据微软发布元数据进行了 SHA-512 校验。
- [WinApp CLI 官方 v0.7.1 发布页](https://github.com/microsoft/WinAppCli/releases/tag/v0.7.1)：使用 `winappcli-x64.zip`，依据发布资产元数据进行了 SHA-256 校验。该版本仍属于官方文档所述的公开预览工具。
- [NuGet](https://www.nuget.org/)：项目依赖由 .NET CLI 根据版本配置和锁文件还原。

下载的安装归档保存在 `artifacts/tooling/`，不提交 Git。SDK 和 CLI 安装在用户目录，NuGet 包保存在包缓存，源码目录中不手工散放开发工具 DLL。

## 验证方式

在项目根目录的 PowerShell 中执行：

```powershell
# 检查 SDK、独立 CLI、Windows SDK 工具与开发者模式状态
.\scripts\check-environment.ps1

# 编译
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Release

# 非打包运行
.\scripts\run.ps1 -Unpackaged
```

本次环境准备已验证 Release / x64 构建及未签名 MSIX 生成。环境自检包位于 `artifacts/packages/FileAssistant-environment-check-x64.msix`；它只验证打包工具链，不是已完成签名、安装测试的发布版本。

Release 构建保留了两条来自 Windows SDK 程序集的 `IL2104` 裁剪警告，构建无错误。它们需要在正式发布优化时评估，不属于缺少开发环境；本次没有为消除警告而隐藏诊断或修改业务代码。

## 下载之外的配置

Windows 开发者模式当前未开启。默认 MSIX 调试部署要求在 Windows 设置中启用开发者模式；`run.ps1 -Unpackaged` 可直接用于当前开发验证。[微软快速入门](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)

Visual Studio 2026 及其 WinUI 工作负载是可选的图形化开发环境。当前命令行路线的创建、构建、运行和打包工具已独立准备；需要使用 Visual Studio 调试器时再安装对应 IDE。[官方两种开发路线](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)

SQLite、7-Zip、图片和 PDF 引擎属于后续产品功能依赖，将在实际接入时选择并锁定版本。它们不影响当前工程的开发环境就绪状态。
