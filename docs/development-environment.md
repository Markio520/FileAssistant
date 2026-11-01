# 开发环境与下载记录

检查日期：2026-10-30（换电脑续接）。当前使用 Windows 11 x64（10.0.26200）上的 .NET / WinUI 命令行开发环境。

2026-10-31 补验：当前完整解决方案 Debug/x64 打包模式和非打包自包含构建通过；实际 Debug/x64 未签名 MSIX 也已生成并回读。MSIX 生成有一条 `mspdbcmf.exe` 缺失警告，未生成符号包；未安装、签名或进行 Release 分发验收。产物、命令和日志见 [格式转换阶段交接](handoff-2026-10-31-format-next.md)，下方保留原环境检查历史。

## 当前所需环境

| 环境或组件 | 版本 | 用途与位置 |
| --- | --- | --- |
| .NET SDK | 10.0.403 | C# 编译器、MSBuild、NuGet CLI；位于 `%LOCALAPPDATA%\Microsoft\dotnet` |
| .NET 与桌面运行时 | 10.0.14 | 随 SDK 安装，支持运行 .NET 桌面程序 |
| WinApp CLI | 0.7.1 | 使用项目 NuGet 包附带的 `tools/win-x64/winapp.exe`；本次未另外安装独立 CLI |
| WinUI C# 模板 | 0.0.7-alpha | 初始化来源；生成代码已提交，新电脑无需重装模板 |
| Windows App SDK | 2.6.4 | 已通过 NuGet 还原；工程版本由 `Directory.Packages.props` 管理 |
| Windows App Runtime | 2.6.4 | 本次非打包运行随输出自包含部署，不依赖全局安装状态 |
| Windows SDK BuildTools | 10.0.28000.2957 | 包含 MakeAppx、MakePRI、SignTool，已验证 x64 工具能启动 |
| CommunityToolkit.Mvvm | 8.4.2 | 已通过 NuGet 还原，提供属性通知与命令生成 |
| 项目依赖 | 由锁文件记录 | 本次 Debug 打包与非打包构建已还原；Release 和 MSIX 发布流程尚未在新电脑复验 |

项目 NuGet 依赖缓存在 `%USERPROFILE%\.nuget\packages`；若设置了自定义缓存位置，以 `dotnet nuget locals global-packages --list` 的输出为准。

本次从微软官方发布元数据取得 SDK 10.0.403 x64 ZIP，完成 SHA-512 校验后安装到上述用户目录，并将该目录加入用户 PATH。新电脑系统目录已有仅含运行时的 `dotnet.exe`，即使用户 SDK 已安装，直接调用 `dotnet` 仍可能选中系统宿主而提示没有 SDK。

`scripts/initialize-dotnet.ps1` 共用于构建、测试、运行和环境检查：先在项目根目录确认 PATH 中的宿主能够解析 `global.json`，失败后尝试文档规定的用户 SDK，随后设置本次脚本进程的 `DOTNET_ROOT` 和 PATH。已在 PowerShell 7 与 Windows PowerShell 5.1 验证系统宿主缺 SDK 时的回退。需要手工运行标准 CLI 时，先执行：

```powershell
. .\scripts\initialize-dotnet.ps1
dotnet --version
```

环境检查支持已安装的独立 WinApp CLI，也可使用 NuGet 缓存内对应版本的 x64 CLI；无需为常规构建和非打包运行重复安装独立 CLI。

## 下载来源与校验

- [.NET 官方下载](https://dotnet.microsoft.com/download/dotnet/10.0)：SDK 压缩包依据微软发布元数据进行了 SHA-512 校验。
- [WinApp CLI 官方 v0.7.1 发布页](https://github.com/microsoft/WinAppCli/releases/tag/v0.7.1)：旧电脑独立安装使用 `winappcli-x64.zip` 并按资产元数据完成 SHA-256 校验；本次直接使用项目 NuGet 包附带的同版本工具。
- [NuGet](https://www.nuget.org/)：项目依赖由 .NET CLI 根据版本配置和锁文件还原。

下载的 SDK 安装归档与发布元数据保存在 `artifacts/tooling/`，不提交 Git。SDK 安装在用户目录，WinApp CLI 与其余 NuGet 包保存在包缓存，源码目录中不手工散放开发工具 DLL。

## 验证方式

在项目根目录的 PowerShell 中执行：

```powershell
# 检查 SDK、可用的 WinApp CLI、Windows SDK 工具与开发者模式状态
.\scripts\check-environment.ps1

# 编译
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Release

# 非打包运行
.\scripts\run.ps1 -Unpackaged
```

2026-10-30 新电脑复验：环境检查通过；123 项常规测试、17 项真实 Everything 引擎测试通过；Debug/x64 打包与非打包自包含构建均为 0 警告、0 错误，并已从 `run.ps1 -Unpackaged` 启动应用。真实文件输入的 8 个集成场景通过。用户授权的个人 Computer Use 允许列表在完整重启桌面客户端后生效，随后完成原生输入与独立列表、搜索设置跨重启检查；系统选择器与跨应用输入由用户补验并确认成功。最新下拉统一高度、已选项竖条、悬停无竖条和动作键盘流程已通过浅/深色原生检查；弹窗主题补丁仍待复验，见[开发交接](handoff-2026-10-30.md)。日志与隔离样本保存在 `artifacts/ui-review-2026-10-30/`，不提交 Git。

2026-10-29 旧电脑曾验证 Release/x64 构建及未签名 MSIX 生成，环境自检包仅保留在旧电脑的忽略目录；它只验证当时的打包工具链，不是当前已完成签名、安装测试的发布版本。

Release 构建保留了两条来自 Windows SDK 程序集的 `IL2104` 裁剪警告，构建无错误。它们需要在正式发布优化时评估，不属于缺少开发环境；本次没有为消除警告而隐藏诊断或修改业务代码。

## 下载之外的配置

Windows 开发者模式当前未开启。默认 MSIX 调试部署要求在 Windows 设置中启用开发者模式；`run.ps1 -Unpackaged` 可直接用于当前开发验证。[微软快速入门](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)

Visual Studio 2026 及其 WinUI 工作负载是可选的图形化开发环境。当前命令行路线的创建、构建、运行和打包工具已独立准备；需要使用 Visual Studio 调试器时再安装对应 IDE。[官方两种开发路线](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)

SQLite、7-Zip、图片和 PDF 引擎属于后续产品功能依赖，将在实际接入时选择并锁定版本。它们不影响当前工程的开发环境就绪状态。

## 本地预览与格式转换引擎（2026-10-30）

已固定 Magick.NET-Q16-x64 15.0.0、PDFiumCore/PDFium 157.0.8100、PdfPig 0.1.16 和 FFmpeg 8.1.3-9-g29e619e767 LGPL shared x64。NuGet 从集中版本及锁文件还原；FFmpeg 的固定 URL、大小、SHA-256、源码和构建配方见 engines/manifest.json，由 scripts/restore-processing-engines.ps1 缓存后随应用复制。当前未安装系统级 FFmpeg，也不要求用户配置 PATH。全部引擎目录、运行文件位置和验证范围见 [本地引擎说明](../engines/README.md)。

139 项常规测试通过；最终 Debug/x64 两种构建模式均零警告、零错误。对实际 UI 生成的两页 PDF 已核对页数、红/蓝顺序、冷缓存渲染和逐页导出，输入原件保持不变。物理断网补验单独记录在开发交接，不以本地引擎测试替代。
