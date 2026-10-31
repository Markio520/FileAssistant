# Windows 文件操作助手

面向 Windows 的免费文件操作助手，聚焦文件与文件夹的批量处理，重视清楚、易用、美观的图形界面，并优先复用成熟开源组件。

代码仓库：[Markio520/FileAssistant](https://github.com/Markio520/FileAssistant)。

项目根目录为当前 `FileAssistant` 文件夹；开发、构建和 Git 命令均从此目录执行。

**换电脑继续开发：先读 [2026-10-30 开发交接](docs/handoff-2026-10-30.md)**，包含启动命令、当前实现和待完成的原生界面验收。

## 当前进度

2026-10-30 最新确认与实现：允许 Everything 作为随软件自动启动的后台引擎。应用现已附带官方稳定版便携引擎，打开软件自动准备本机索引，不再要求用户另装或手动启动 Everything。

已使用微软官方 WinUI MVVM 模板和 .NET 类库模板初始化三个源码项目，并建立解决方案、项目引用、SDK 版本约束、集中依赖配置和构建脚本。

主工作区设计与基础 WinUI 界面已实现，已替换模板计数器。包含左侧导航、中央文件列表、右侧预览与复制参数、底部可收起的任务区，以及浅色、深色和跟随系统的外观。设计与验收说明见 [主工作区设计](docs/workspace-design.md)。

搜索与筛选第一阶段已接入真实文件：Everything 本地索引查询、指定目录扫描、文件暂存区范围搜索，支持名称/路径、类型、大小、修改日期、基础表达式、排序和每页 200 项。结果多选后可加入暂存区，重新校验路径并报告添加结果。详见 [搜索设计与使用说明](docs/search-design.md)。

进入搜索页默认显示“所有类型”的文件，清空条件后恢复全部结果，按 200 项分页加载；指定目录选择文件夹后即展示目录内容，无需关键词。类型包含所有、音频、压缩文件、文档、可执行文件、文件夹、图片、视频，另保留仅文件。范围/类型/大小/日期/排序及工作区选择框共用 `SelectionDropDown`：按钮固定、弹层下方居中，选中行以主题色竖条和浅色背景表示，没有圆点及黑色焦点框。配色集中在 `Resources/ThemePalette.xaml`，公共样式集中在 `Resources/SelectionControls.xaml`。表头及各列内容统一左对齐、分隔线可拖动。右键支持打开文件/文件夹、打开所在文件夹、复制完整路径、复制文件名及加入暂存区；复制与加入暂存区支持多选，双击或 Enter 可打开单个结果。

首次全盘搜索需要 Windows 管理员授权，安装本软件专用的磁盘访问辅助服务；主界面和查询进程仍以普通权限运行。后台引擎使用独立实例、配置和索引，关闭窗口时保存索引并退出，不改动用户原有 Everything。辅助服务保留供下次运行使用。取消授权时可用指定目录，之后通过“重试搜索”重新授权。查询无需联网；输入或刷新读取最新索引，目录扫描仍使用可刷新的快照。

搜索体验优化已完成前两步：新查询成功后再替换旧列表；切换页面保留结果、选择与滚动；翻页失败可重试且已有文件仍可操作。底部常驻匹配、已加载、已选计数和全选范围；点击表头切换升降序，大小和日期支持自定义单侧或双侧区间。列宽、排序、信息栏开关自动跨重启保存，常用条件可命名保存、套用和移除。设置保存在 `%LOCALAPPDATA%\FileAssistant\search-settings.json`，不保存文件结果或选择。

搜索输入采用 150 ms 防抖并保留中文输入法组合保护；扫描进度在进入界面队列前按 100 ms 节流；翻页防止重复在途请求；设置变更合并 400 ms 写入并在关闭时补写最终状态。

表格列宽与右侧栏现共用公共拖拽控件：暂存区、复制与整理、格式转换的文件名/所在位置、类型、大小列可拖动，表头与行同步，超出空间时横向滚动。搜索、上述输入页及文件输出区的右侧栏均可拖动调宽，并可通过“文件信息”或“侧栏”按钮收起/恢复；恢复保留本次宽度，窄窗口也保留开关。新增输入列宽和侧栏宽度仅在本次运行内保留，搜索原有列宽与开关持久化继续有效。

转换格式菜单按输入动态筛选：有选择时只处理选中项，未选择时处理整个输入列表；显示共同可转换的目标并隐藏同格式输出。图片支持 PNG/JPG/WebP/BMP/TIFF/GIF/AVIF，另可合并 PDF；PDF 可按页导出七种图片。音频支持 MP3/WAV/FLAC/M4A/AAC/OGG 输出，视频支持 MP4/MKV/MOV/WebM 输出。DOC/DOCX/ODT/RTF/TXT 互转及单份 PDF 输出由按需 LibreOffice 引擎提供，在设置中下载约 373 MB 后使用；不借用系统 Office。图片与文档混选提示按类型分批，缺少引擎时提供安装入口。输入、选择或参数改变会使旧计划失效。GIF 为静态输出，视频只保留首个视频流和首个音轨，文档字体/复杂排版需核对，范围详见产品方案 §5.3 与 [引擎文档](engines/README.md)。

第三步文件暂存区已接入代码：统一名称，提供选择文件/文件夹、拖入、粘贴文件或逐行完整路径；加入只保存引用，文件夹按本身加入。添加时校验路径、去重，并提供重复、不支持、失效项目的明细。“复制与整理”使用独立输入列表，可直接添加、选择暂存区文件导入，或显式把选中项加入暂存区；导入为追加，不触发执行，两边移出或清空互不影响。共享输入能力位于 `Features/FileInput`，后续功能复用。

暂存区仍保留明确标注的界面示例，真实文件加入后移除示例，示例不可导入功能。暂存区与功能输入均只保存在本次运行内存中，不复制、移动或修改原件。类型筛选与搜索共用九类选项和过滤规则；添加、用于功能等动作复用公共下拉样式及统一高度，绿条仅表示已选项，悬停只显示背景。文件输入、独立交换及搜索设置恢复的原生流程已检查，用户补验的选择文件/文件夹、拖入、资源管理器复制后粘贴均成功；最新公共下拉已检查浅/深色与键盘，弹窗主题补丁仍待原生复验。真实图片、视频首帧、PDF 首页及部分文本预览已接入；复制与改名执行仍未接入。

打开右上角「界面示例」可切换各状态及标准/紧凑窗口；调色板按钮可选择浅色、深色或跟随系统。外观选择目前只在本次运行中有效。列表支持 Ctrl+A 全选，Delete 只移出列表。格式转换支持预览计划、执行、取消和逐项结果；复制参数仍为界面预览。

当前版本在 Windows 11 x64 的常规测试 176 项（Core 57、Infrastructure 45、App 74）及按需文档引擎测试 8 项均通过；Debug/x64 打包与非打包构建 0 警告、0 错误。覆盖动态菜单、选择范围、真实格式转换、原件保护、取消和引擎安装；Everything 集成测试 17 项沿用此前验收，本轮未重跑。原生检查和物理断网验收边界见[开发交接](docs/handoff-2026-10-30.md)。本机 C、D 两个 NTFS 磁盘的实际索引约 175 万项；这是本机功能验证，不是全面的性能或完整性基准。

此前模板阶段已验证 Release 构建和未签名 MSIX 生成，详见环境文档；这不代表当前后台索引的 Release、MSIX 安装、提权和卸载流程已经通过发布验收。当前引擎获取脚本仅开放已验证的 x64，其他处理器架构明确报错。

`FileAssistant` 是暂用的代码前缀，产品名称尚未确定。空目录中的 `.gitkeep` 用于让 Git 保留目录；添加实际文件后可移除对应占位文件。

常用本地处理引擎已接入：Magick.NET 15.0.0、FFmpeg 8.1.3 LGPL shared、PDFiumCore/PDFium 157.0.8100、PdfPig 0.1.16，以及按需 LibreOffice 26.2.6.3。格式转换拥有独立输入，支持上述图片、文本文档和音视频格式，以及图片合并 PDF 和 PDF 逐页导出。成功产物进入“文件输出区”，可按任务筛选、打开、定位、复制路径并显式交给暂存区或其他功能；清空记录保留磁盘文件。任务队列显示真实进度、取消和逐项结果。设置提供引擎状态检查、音视频引擎修复和文档引擎按需安装。物理断网且空缓存验收仍未完成，已通过的本机测试不等同于该项验收。代码和运行文件的分层、调用示例与能力边界见 [本地引擎说明](engines/README.md)。
## 项目文档

- [产品与技术方案](./文件操作助手-产品与技术方案.md)：功能范围、界面要求、技术候选与开发顺序。
- [代码架构与目录规范](./docs/architecture.md)：项目职责、依赖方向和代码摆放规则。
- [项目协作约定](./AGENTS.md)：已确认要求、最新范围纠正和协作规范。
- [第三方组件与声明](./third-party/README.md)：已接入组件、模板来源和许可记录。
- [开发环境与下载记录](./docs/development-environment.md)：已准备的环境、安装位置和验证结果。
- [主工作区设计与验收](./docs/workspace-design.md)：当前界面布局、交互范围与验证记录。
- [搜索与筛选](./docs/search-design.md)：真实搜索范围、语法、引擎依赖与验证记录。

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
├─ engines/                           # 引擎版本清单与公共调用说明
├─ packaging/                         # 发布配置与安装资源
├─ third-party/                       # 第三方组件清单与许可声明
└─ artifacts/                         # 构建产物，已被 Git 忽略
```

App 已按工作区、搜索、预览、创建与改名、整理、转换、压缩、校验、清理、任务和设置预留功能目录。“批量创建”和“批量改名”已合并为单个“批量创建与改名”导航入口及 `CreateOrRename` 模块，实际处理功能仍待接入。各功能的页面与 ViewModel 放在对应目录内，业务规则放在 Core，实际文件访问及组件调用放在 Infrastructure。

项目引用遵循 `App → Core`、`Infrastructure → Core`；App 仅在启动组装处使用 Infrastructure 的具体实现。测试、打包配置及构建产物分别放置。

## 开发环境

| 项目 | 当前版本与配置 |
| --- | --- |
| .NET SDK | 10.0.403；`global.json` 允许同一功能带的补丁升级，不接受预发布 SDK |
| WinUI 模板包 | Microsoft.WindowsAppSDK.WinUI.CSharp.Templates 0.0.7-alpha；仅初始化时需要 |
| Windows App SDK | 2.6.4 |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.2 |
| WinApp 构建集成 | Microsoft.Windows.SDK.BuildTools.WinApp 0.7.1；随 NuGet 依赖还原，无需另装全局 CLI |
| WinApp CLI | 0.7.1；项目 NuGet 包已附带，独立安装为可选项 |

本机 SDK 安装在 `%LOCALAPPDATA%\Microsoft\dotnet`，已加入当前用户的 PATH；脚本支持从该用户目录查找 SDK。其他机器应安装 `global.json` 对应的 .NET SDK，首次还原需要连接 NuGet。

2026-10-30 换机复验发现系统自带的仅运行时宿主可能优先于用户 SDK。项目脚本现通过 `scripts/initialize-dotnet.ps1` 验证所需 SDK 并自动回退到用户安装目录；手工使用 `dotnet` 前可先运行 `. .\scripts\initialize-dotnet.ps1`。环境检查也能复用 NuGet 自带的 WinApp CLI。新电脑已通过 121 项常规测试、17 项真实引擎测试与两种 Debug/x64 构建，另有 8 个真实文件输入集成场景通过；用户已授权原生界面访问，但控制工具仍拒绝，待客户端重新加载配置后续接，未视为界面验收完成。

当前完整环境清单见[开发环境与下载记录](./docs/development-environment.md)，可运行 `scripts/check-environment.ps1` 检查本机工具。

模板版本带有 alpha 标记；生成的源码已经保存在仓库内，常规构建无需重新运行模板。初始化来源和具体组件许可见[第三方说明](./third-party/README.md)。

## 构建与运行

在项目根目录的 PowerShell 中执行：

```powershell
# 编译解决方案，默认 Debug / x64
.\scripts\build.ps1

# 运行主工作区，不要求开启 Windows 开发者模式
.\scripts\run.ps1 -Unpackaged

# 运行常规规则、目录扫描及界面状态测试
.\scripts\test.ps1

# 下载并校验官方便携版，仅索引生成的测试文件；测试后退出独立实例
.\scripts\test-everything.ps1
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
