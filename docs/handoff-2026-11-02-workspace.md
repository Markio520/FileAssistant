# 工作区改版与公共交互交接

更新日期：2026-11-02（Asia/Shanghai）。本交接随本轮源码提交保存，继续开发先读根目录 `AGENTS.md` 和本文件。转换引擎背景见 [上一阶段交接](handoff-2026-10-31-format-next.md)，具体能力以 [支持格式表](conversion-formats.md) 为准。

## 当前交付状态

用户已授权本轮提交并推送 GitHub，仓库 `Markio520/FileAssistant`，分支 `main`。本轮起点为 `8a6f66b`；此次保存界面改版、文件夹树和公共输入交互、预览/转换错误提示及相关测试文档。授权只适用于此次提交，不形成永久自动提交、推送或重启规则。

最新源码已通过 Debug/x64 打包与非打包自包含构建，并更新原本地运行目录。最后两项调整为“返回上一级移到文件夹栏头”和“搜索文字上下居中、保持靠左”。更新后的程序已经启动；最后一次读取窗口时工具检测到用户正在操作，未完成这两项的原生显示复验。不得把构建成功、程序启动或用户正在检查视作界面验收通过。

## 本轮实际实现

- 顶部使用横向功能导航和填充选中态；文件输出、任务队列、外观及设置保留入口。左右侧栏可收起并通过 48 DIP 窄栏恢复，主工作面与右栏间隔 20 DIP。
- 输入默认显示真实缩略图，可切换表格并保留选择。公共空白、加载、错误和无结果状态在内容区域居中，窗口过矮时可滚动。
- 颜色集中在 `Resources/ThemePalette.xaml`；语义画刷与 Fluent 覆盖在 `WorkspaceTheme.xaml`。公共导航、图标按钮、工作面、文件夹及文件项样式在 `WorkspaceControls.xaml`，下拉样式继续使用 `SelectionControls.xaml`。修改配色时同步维护浅色、深色与高对比度资源。
- 文件夹导航使用以“全部文件”为根的树，按层级缩进并显示递归去重数量。右键目录新建子级，右键根或空白新建顶层；新建目标取右键对象。已移除常驻“新建软件文件夹”按钮。
- 左侧软件文件夹可拖进其他目录成为子级，拖到根或空白可移回顶层；复用原有循环检查、引用保存与撤销。原生拖放事件可能被重定向到外层 Grid，目标通过目录行实际边界命中，不能仅依赖 `OriginalSource`。
- 公共 `AddFilesButton` 提供添加文件/文件夹菜单，供页头与空状态复用。公共 `DropTargetHighlight` 保存并恢复原背景来源，避免拖放反馈覆盖主题或选中样式。
- 公共 `MarqueeSelection` 用于三个输入视图、搜索结果和输出列表，支持从空白处起拖、Ctrl/Shift 追加、Esc 恢复及边缘滚动。输入工具栏留白与首行上方也能起拖；按钮、输入框、表头和拖动分隔区保留各自交互。
- 框选按渲染帧更新并缓存条目几何位置，滚动/尺寸变化后刷新。拖动期间只更新原生选择高亮，业务预览和转换范围在 `SelectionCommitted` 时统一提交；普通点击仍立即响应。后续修改不得恢复每次指针移动重算预览/计划的方式。
- 工具栏采用最新 IMG 概念图 B“一体搜索栏”：左侧目录图标、名称和可见项数，中间合并类型筛选与搜索，右侧集中视图、用于功能/从暂存区导入和操作。此前 HTML 候选中的 B 未采用。中央工具栏宽度低于 760 DIP 时搜索换到第二行，低于 520 DIP 时操作再分行，表头只在列表模式显示。
- 后续反馈已将返回上级移至左侧文件夹栏头；侧栏收起时窄栏也保留入口。公共 `FilterSearchBox` 复用已有 `SelectionDropDown` 和原生搜索输入，搜索文字上下居中并保持靠左，焦点与边框取公共主题。
- 输入底部不再常驻添加摘要/保存提示/撤销横条；撤销、明细、保存重试等移到“操作”菜单。任务队列通过全局浮层打开，不占列表底部。列表底边仍可拖动调整高度，双击恢复填满；该高度只在本次会话保留。
- 转换栏保留真实文件预览、常用参数、默认展开且可折叠的转换计划和输出目录，开始转换固定在底部。活动转换页的参数/输入变化以 400 ms 防抖更新计划，旧计划失效；仍须用户点击开始，未增加自动执行转换。
- 本地处理按 AppleDouble 文件头识别 macOS 元数据并提供可操作的原文件提示，不只凭 `._` 名称拒绝文件。真实图片即便带类似名称仍可处理；混合批次保留可用项，损坏图片采用简明错误文案。

## 代码入口

| 入口 | 维护职责 |
| --- | --- |
| `Features/Workspace/WorkspacePage.*`、`WorkspaceTaskPanel.*` | 功能导航、任务浮层与页面入口 |
| `Features/FileInput/FileInputView.xaml` | 公共输入工作面与工具栏 |
| `FileInputView.Display.cs` | 缩略图/列表、侧栏开关、工具栏分行 |
| `FileInputView.Folders.cs` | 文件夹树同步、上下文新建与树拖放 |
| `FileInputView.Layout.cs`、`Controls/ResizeHandle.cs`、`ResizablePane.*` | 列表高度、列宽和侧栏尺寸 |
| `Controls/MarqueeSelection.cs` | 框选起点、几何缓存、自动滚动与结束提交 |
| `Controls/FilterSearchBox.*`、`AddFilesButton.cs`、`DropTargetHighlight.cs` | 公共筛选搜索、文件入口与拖放反馈 |
| `Features/Preview/FileThumbnail.*`、`FilePreviewView.*` | 缩略图、预览及错误状态 |
| `Features/Convert/ConversionOptionsView.*`、`ConversionViewModel.cs` | 参数、自动计划检查与显式执行 |
| `Infrastructure/Engines/LocalFileProcessingService.cs` | 元数据文件识别与处理错误转换 |

## 验证结果与边界

提交前通过 `scripts/test.ps1` 重新运行常规测试：**256 项通过，0 失败，0 跳过**，分别为 Core 75、Infrastructure 76、App 105。新增覆盖文件夹真实拖放数据、移入/移回顶层、成员引用保持、祖先链和计数、非法移动无部分变化，以及 AppleDouble、类似名称真实图片和损坏文件处理。状态测试不能替代 WinUI 鼠标验收。

最新 Debug/x64 打包工程与原非打包自包含构建均为 **0 警告、0 错误**。这次只提交源码、测试、文档与设计资源；未重新生成或发布正式安装包。Office 25 项及 Everything 17 项是上一阶段记录，本次未重跑，不并入本轮 256 项。

本轮此前的原生检查已观察真实缩略图与右侧预览、自动计划与折叠、视图切换保留选择、左右栏收起恢复、浅深色切换、右键新建父级定位和树折叠。从首行上方留白框选 6 个文件以及从空白反向框选两个缩略图已观察。以上只代表实际检查过的场景，不是所有功能的完整验收。

最后更新前已保留最新集合。更新后暂存区与转换集合的 `Folders` 和 `Entries` 比较一致；未用旧快照覆盖用户在检查期间修改的目录层级或当前位置。不要把当前位置变化造成的 JSON 哈希变化误判成文件引用丢失，也不要为恢复旧哈希而覆盖用户操作。

下一次优先验收：

1. 最新返回上级位置、侧栏折叠后的上级入口，以及占位/输入文字的垂直居中与靠左；确认 B 版在宽、标准、紧凑窗口的分行与完整菜单可用。
2. 长按慢速拖动文件夹进入另一目录，再拖回根与空白；检查目标高亮、跨节点、循环拒绝、撤销及重启恢复。自动化快速单次拖动不等同于完整原生拖放。
3. 多文件持续框选、跨边缘滚动、Ctrl/Shift 追加、Esc 取消，以及搜索/输出列表上的相同行为；结合实际文件量观察卡顿，尚无帧率或提速比例保证。
4. 复验列表高度拖动、任务浮层、深色/高对比度、缩放及极矮窗口；历史局部检查不能替代新布局的完整组合测试。
5. 严格物理断网、Release、MSIX 签名/安装/提权/卸载及分发材料审计仍待完成。断网前准备引擎和测试程序集，再使用 `scripts/test.ps1 -Offline`；当前联网测试不算断网通过。

## 本机运行与续接命令

当前原运行入口为：

```text
src/FileAssistant.App/bin/unpackaged/x64/Debug/net10.0-windows10.0.26100.0/win-x64/FileAssistant.App.exe
```

本机项目根目录为 `C:/Users/26237/Documents/ChatGPT/文件批处理/FileAssistant`。从根目录运行 `scripts/run-next-stage.ps1` 可启动现有构建；脚本在已有实例时拒绝启动。需要重建时正常关闭现有程序以补写集合，然后执行：

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/run.ps1 -Unpackaged
```

直接重建原运行目录的等价命令为：

```powershell
. ./scripts/initialize-dotnet.ps1
& $taskDotnet build src/FileAssistant.App/FileAssistant.App.csproj --configuration Debug -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --nologo
```

集合位于 `%LOCALAPPDATA%/FileAssistant/Collections/{staging,copy,conversion}.json`，保存软件分组和文件引用，不保存磁盘原件内容。更新时保留最新数据；不要强制终止正在补写的进程，也不要把验收快照当作固定恢复源。

本机证据位于忽略目录：`artifacts/handoff-20261102/standard-tests.log`、`artifacts/toolbar-b-20261102/build-alignment-{packaged,unpackaged}.log` 与 `collection-alignment-check.json`。前序检查分别位于 `artifacts/ui-redesign-20261102/`、`folder-tree-20261102/`、`input-interactions-20261102/`、`marquee-fix-20261102/`。概念图、旧独立构建、缓存、用户集合、生成图片及日志不作为源码上传；`.impeccable/questions/` 为本地候选抽取状态，也已忽略。

复制与改名执行、任务/输出跨重启历史、未来配色设置页仍未实现。体积优化按用户此前决定留到正式打包；现有许可与分发待办见上一阶段交接，不因本轮界面提交而扩大已实现范围。
