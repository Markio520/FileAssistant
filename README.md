# Windows 文件操作助手

面向 Windows 的免费文件操作助手，聚焦文件与文件夹的批量处理，重视清楚、易用、美观的图形界面，并优先复用成熟开源组件。

代码仓库：[Markio520/FileAssistant](https://github.com/Markio520/FileAssistant)。

项目根目录为当前 `FileAssistant` 文件夹；开发、构建和 Git 命令均从此目录执行。

## 当前进度

已使用微软官方 WinUI MVVM 模板和 .NET 类库模板初始化三个源码项目，并建立解决方案、项目引用、SDK 版本约束、集中依赖配置和构建脚本。

当前应用保留模板的计数器示例，用于验证窗口、MVVM 命令及属性绑定。文件操作功能和正式产品界面尚未实现；测试目录仍为预留目录，业务测试随功能添加。

已在 Windows 11 x64（系统版本 10.0.26200）上验证：Debug 解决方案构建通过，零警告、零错误；非打包模式窗口成功启动，计数器从 0 增加到 1，再减少到 0，窗口正常关闭。Release 构建和未签名 MSIX 生成也已通过；Release 存在两条 Windows SDK 程序集裁剪警告，详见环境文档。MSIX 安装、签名发布及其他处理器架构尚未验证。

`FileAssistant` 是暂用的代码前缀，产品名称尚未确定。空目录中的 `.gitkeep` 用于让 Git 保留目录；添加实际文件后可移除对应占位文件。

## 项目文档

- [产品与技术方案](./文件操作助手-产品与技术方案.md)：功能范围、界面要求、技术候选与开发顺序。
- [代码架构与目录规范](./docs/architecture.md)：项目职责、依赖方向和代码摆放规则。
- [项目协作约定](./AGENTS.md)：已确认要求、最新范围纠正和协作规范。
- [第三方组件与声明](./third-party/README.md)：已接入组件、模板来源和许可记录。
- [开发环境与下载记录](./docs/development-environment.md)：已准备的环境、安装位置和验证结果。

## 已建立的目录

```text
FileAssistant/
├─ FileAssistant.slnx                 # 解决方案入口
├─ global.json                        # .NET SDK 版本约束
├─ Directory.Build.props              # 统一编译、锁文件及输出配置
├─ Directory.Packages.props           # NuGet 直接依赖版本
├─ src/
│  ├─ FileAssistant.App/               # 界面、ViewModel、资源与启动组装
│  ├─ FileAssistant.Core/              # 业务规则、操作计划与流程
│  └─ FileAssistant.Infrastructure/    # 文件系统、Windows 与第三方组件适配
├─ tests/
│  ├─ FileAssistant.Core.Tests/
│  ├─ FileAssistant.Infrastructure.Tests/
│  └─ FileAssistant.App.Tests/
├─ docs/                              # 架构及专项文档
├─ scripts/                           # 开发、构建与打包脚本
├─ packaging/                         # 发布配置与安装资源
├─ third-party/                       # 第三方组件清单与许可声明
└─ artifacts/                         # 构建产物，已被 Git 忽略
```

App 已按工作区、搜索、预览、创建、改名、整理、转换、压缩、校验、清理、任务和设置预留功能目录。各功能的页面与 ViewModel 放在对应目录内，业务规则放在 Core，实际文件访问及组件调用放在 Infrastructure。

项目引用遵循 `App → Core`、`Infrastructure → Core`；App 仅在启动组装处使用 Infrastructure 的具体实现。测试、打包配置及构建产物分别放置。

## 开发环境

| 项目 | 当前版本与配置 |
| --- | --- |
| .NET SDK | 10.0.403；`global.json` 允许同一功能带的补丁升级，不接受预发布 SDK |
| WinUI 模板包 | Microsoft.WindowsAppSDK.WinUI.CSharp.Templates 0.0.7-alpha；仅初始化时需要 |
| Windows App SDK | 2.6.4 |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.2 |
| WinApp 构建集成 | Microsoft.Windows.SDK.BuildTools.WinApp 0.7.1；随 NuGet 依赖还原，无需另装全局 CLI |
| 独立 WinApp CLI | 0.7.1；已安装到用户目录，供直接调用 `winapp` 使用 |

本机 SDK 安装在 `%LOCALAPPDATA%\Microsoft\dotnet`，已加入当前用户的 PATH；重新打开终端后可直接使用 `dotnet`。脚本也支持从该用户目录查找 SDK。其他机器应安装 `global.json` 对应的 .NET SDK，首次还原需要连接 NuGet。

当前完整环境清单见[开发环境与下载记录](./docs/development-environment.md)，可运行 `scripts/check-environment.ps1` 检查本机工具。

模板版本带有 alpha 标记；生成的源码已经保存在仓库内，常规构建无需重新运行模板。初始化来源和具体组件许可见[第三方说明](./third-party/README.md)。

## 构建与运行

在项目根目录的 PowerShell 中执行：

```powershell
# 编译解决方案，默认 Debug / x64
.\scripts\build.ps1

# 运行已验证的非打包示例，不要求开启 Windows 开发者模式
.\scripts\run.ps1 -Unpackaged
```

开发者模式已开启时，可使用模板默认的 MSIX 调试启动方式：

```powershell
.\scripts\run.ps1
```

两种运行方式的差别是包身份和运行时部署方式，不是产品功能版本。非打包调试复制 Windows App SDK 运行时到输出目录，输出体积相应更大。正式安装包将在发布阶段单独验证。

构建脚本支持 `-Configuration Debug|Release` 和 `-Architecture x64|x86|ARM64`；目前已验证 Debug 和 Release 的 x64 构建。也可以直接使用标准 CLI：

```powershell
dotnet build FileAssistant.slnx -c Debug -p:Platform=x64
```

解决方案里的纯类库使用 Any CPU 配置，WinUI 应用明确选择处理器架构。修改包版本时更新 [Directory.Packages.props](./Directory.Packages.props)，然后重新还原并提交各源码项目的 `packages.lock.json`。

## 产物与调试目录

- WinUI 打包模式：`src/FileAssistant.App/bin/packaged/`，中间文件位于对应的 `obj/packaged/`。
- WinUI 非打包模式：`src/FileAssistant.App/bin/unpackaged/`，中间文件位于对应的 `obj/unpackaged/`。
- 两种模式需要不同的资源索引，分别存放可避免切换模式时复用错误的 WinUI 主题资源缓存。
- 类库编译产物使用各自的 `bin/`、`obj/`；上述产物均不提交 Git。
- 安装包、诊断报告等导出产物放 `artifacts/`；该目录按需创建，不保存源码或发布配置。
