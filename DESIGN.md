---
name: "FileAssistant（暂用名）"
description: "当前 WinUI 主工作区的原生控件、主题与布局记录"
colors:
  workspace-accent-light: "#006C70"
  workspace-accent-dark: "#83D5D7"
  accent-fill-secondary-light: "#087C80"
  accent-fill-secondary-dark: "#99DFE0"
  accent-fill-tertiary-light: "#146367"
  accent-fill-tertiary-dark: "#6FC2C5"
  workspace-accent-subtle-light: "#E5F2F2"
  workspace-accent-subtle-dark: "#234345"
  selection-hover-light: "#D7EAEB"
  selection-hover-dark: "#2C5153"
  workspace-background-light: "#F3F5F7"
  workspace-background-dark: "#1B2025"
  workspace-surface-light: "#FFFFFF"
  workspace-surface-dark: "#252C32"
  workspace-subtle-light: "#F7F9FA"
  workspace-subtle-dark: "#20272D"
  workspace-border-light: "#DEE4E8"
  workspace-border-dark: "#3A444D"
  workspace-secondary-light: "#56636F"
  workspace-secondary-dark: "#B6C1CB"
  workspace-success-light: "#18703F"
  workspace-success-dark: "#8CD8AC"
  system-accent: "#007D82"
  system-accent-light1: "#5CB8BF"
  system-accent-light2: "#83D5D7"
  system-accent-light3: "#C4ECEE"
  system-accent-dark1: "#006C70"
  system-accent-dark2: "#005A60"
  system-accent-dark3: "#003F44"
typography:
  headline:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "28 DIP"
    fontWeight: 600
  empty-title:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "22 DIP"
    fontWeight: 600
  section-title:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "20 DIP"
    fontWeight: 600
  detail-title:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "18 DIP"
    fontWeight: 600
  section:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "14 DIP"
    fontWeight: 600
  supporting:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "13 DIP"
    fontWeight: 400
  caption:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "12 DIP"
    fontWeight: 400
rounded:
  badge: "4 DIP"
  file-row: "6 DIP"
  panel: "8 DIP"
  work-surface: "10 DIP"
spacing:
  space-8: "8 DIP"
  space-12: "12 DIP"
  space-16: "16 DIP"
  space-18: "18 DIP"
  space-20: "20 DIP"
  space-24: "24 DIP"
components:
  button-primary-light:
    backgroundColor: "{colors.workspace-accent-light}"
  button-primary-dark:
    backgroundColor: "{colors.workspace-accent-dark}"
  work-surface-light:
    backgroundColor: "{colors.workspace-surface-light}"
    rounded: "{rounded.work-surface}"
  work-surface-dark:
    backgroundColor: "{colors.workspace-surface-dark}"
    rounded: "{rounded.work-surface}"
  file-row:
    rounded: "{rounded.file-row}"
    padding: "10,8 DIP"
  file-row-selected-light:
    backgroundColor: "{colors.workspace-accent-subtle-light}"
  file-row-selected-dark:
    backgroundColor: "{colors.workspace-accent-subtle-dark}"
  sample-badge-light:
    backgroundColor: "{colors.workspace-accent-subtle-light}"
    textColor: "{colors.workspace-accent-light}"
    rounded: "{rounded.badge}"
    padding: "12,6 DIP"
  sample-badge-dark:
    backgroundColor: "{colors.workspace-accent-subtle-dark}"
    textColor: "{colors.workspace-accent-dark}"
    rounded: "{rounded.badge}"
    padding: "12,6 DIP"
  preview-marker-light:
    backgroundColor: "{colors.workspace-accent-subtle-light}"
    rounded: "{rounded.panel}"
    height: "168 DIP"
  preview-marker-dark:
    backgroundColor: "{colors.workspace-accent-subtle-dark}"
    rounded: "{rounded.panel}"
    height: "168 DIP"
  task-panel-light:
    backgroundColor: "{colors.workspace-surface-light}"
    rounded: "{rounded.panel}"
  task-panel-dark:
    backgroundColor: "{colors.workspace-surface-dark}"
    rounded: "{rounded.panel}"
---

# Design System: FileAssistant（暂用名）

## Overview

**Creative North Star: "清晰的文件工作台"**

当前界面以文件列表为主要工作面，用冷灰背景、分层工作面与青色强调建立层级。原生 Fluent 控件承担输入、选择、菜单和反馈；选中行与右侧文件标记使用同一组强调资源，帮助用户核对当前对象。

本记录于 2026-10-29 从现有 XAML/C# 提取。上述名称与描述概括本次实现，不代表用户已确认永久品牌；正式产品名称仍未确定。产品边界见 [PRODUCT.md](PRODUCT.md)，当前页面的布局与验收记录见 [主工作区设计](docs/workspace-design.md)。这里的尺寸单位是 Windows DIP，字体资源名称是 WinUI 标识，不能按 CSS 字体或物理像素解释。

**Key Characteristics:**

- 原生 Fluent 控件与系统中文字体。
- 冷灰工作面、青色强调和细分隔线。
- 文件名称与路径优先，选中状态联动详情。

## Colors

### Primary

浅色采用深青强调，深色采用浅青强调。前置 token 的 `-light` / `-dark` 是主题配对；颜色值集中在 [ThemePalette.xaml](src/FileAssistant.App/Resources/ThemePalette.xaml)，由 [WorkspaceTheme.xaml](src/FileAssistant.App/Resources/WorkspaceTheme.xaml) 映射成共享画刷。

| 资源 | 前置 token（分别追加 `-light` / `-dark`） | 当前用途 |
| --- | --- | --- |
| `WorkspaceAccentBrush` | `workspace-accent` | 文件图标、预览标记、示例标签文字和进度 |
| `AccentFillColorDefaultBrush`、`AccentTextFillColorPrimaryBrush`、`NavigationViewSelectionIndicatorForeground` | `workspace-accent` | Fluent 强调控件与导航选择指示 |
| `AccentFillColorSecondaryBrush` | `accent-fill-secondary` | Fluent 控件的次级强调填充状态 |
| `AccentFillColorTertiaryBrush` | `accent-fill-tertiary` | Fluent 控件的三级强调填充状态 |
| `WorkspaceAccentSubtleBrush`、`ListViewItemBackgroundSelected` | `workspace-accent-subtle` | 预览标记、示例标签背景和选中行 |
| `ListViewItemBackgroundSelectedPointerOver` | `selection-hover` | 指针停留在选中行时的填充 |

[ThemePalette.xaml](src/FileAssistant.App/Resources/ThemePalette.xaml) 集中维护应用颜色及 `SystemAccentColor` 的 `Light1`–`Light3`、`Dark1`–`Dark3` 色阶。`AppAccentColor` 在浅色引用 Dark1、深色引用 Light2，语义画刷、Fluent 强调控件及筛选竖条共用该值。`App.xaml` 只合并资源，不再另存颜色副本。当前仍是应用内青色，不修改 Windows 设置；本次建立统一换色入口，尚未提供用户配色设置页。

### Neutral

| 资源 | 前置 token（分别追加 `-light` / `-dark`） | 当前用途 |
| --- | --- | --- |
| `WorkspaceBackgroundBrush` | `workspace-background` | 窗口与导航背景 |
| `WorkspaceSurfaceBrush` | `workspace-surface` | 中央工作面与任务区 |
| `WorkspaceSubtleBrush` | `workspace-subtle` | 列标题与右侧检查器背景 |
| `WorkspaceBorderBrush` | `workspace-border` | 容器轮廓与分隔线 |
| `WorkspaceSecondaryBrush` | `workspace-secondary` | 路径、说明、计数与元数据标签 |

普通正文、禁用内容、焦点指示及错误色沿用 Fluent 主题资源；错误图标使用 `SystemFillColorCriticalBrush`。`workspace-success` 对应已定义的 `WorkspaceSuccessBrush`，当前工作区尚未消费该资源，不表示已有成功结果界面。

高对比度字典把背景、工作面、次级工作面和浅强调背景映射到 `SystemColorWindowColor`，边框、次级文字与成功资源映射到 `SystemColorWindowTextColor`，强调映射到 `SystemColorHotlightColor`；该字典未另行覆盖 Fluent 的强调填充及选中行资源。这里只记录已有映射，不构成高对比度验收结论。标题栏按钮另由 [MainWindow.xaml.cs](src/FileAssistant.App/MainWindow.xaml.cs) 设置透明背景，浅/深主题分别使用 Black/White 前景与 DimGray/LightGray 非活动前景。

## Typography

文本继承 WinUI 的 `XamlAutoFontFamily`；应用没有自定义字体文件或 `FontFamily` 覆盖。前置 token 用该资源名表达实际继承关系，不固定为某一款中文字体。正文和标准按钮等未显式设定的字号继续使用 Fluent 默认值，行高与字距也没有项目级覆盖。

| Token | 当前角色 |
| --- | --- |
| `headline` | 工作区页标题 |
| `empty-title` | 初始空列表标题 |
| `section-title` | 无匹配、错误与复制参数标题 |
| `detail-title` | 选中文件名称 |
| `section` | 参数提示与空任务区标题（`WorkspaceSectionStyle`） |
| `supporting` | 检查器说明与任务区辅助说明 |
| `caption` | 路径、列标题、元数据、计数与示例标签（`WorkspaceCaptionStyle`） |

加载文案使用常规字重（18 DIP），不复用半粗的 `detail-title`。列表中文件名半粗、路径较弱；文件名与路径采用尾部省略，完整路径通过原生工具提示提供。检查器中的名称和完整路径可换行、可选中文本。图标使用原生 `FontIcon` / `FontIconSource`，不属于正文字体层级。

## Layout

以下是当前主工作区的数值，不自动成为未来所有页面的布局要求。依据 [MainWindow](src/FileAssistant.App/MainWindow.xaml.cs) 与 [WorkspacePage](src/FileAssistant.App/Features/Workspace/WorkspacePage.xaml)：

| 项目 | 当前值与行为 |
| --- | --- |
| 窗口目标尺寸 | 标准（1320 × 820 DIP）；界面示例中的紧凑窗口（960 × 640 DIP） |
| 最小尺寸 | 首选下限（940 × 600 DIP），按显示器工作区限制 |
| DPI 与工作区 | 窗口尺寸按 `GetDpiForWindow / 96` 转换为物理像素；初始宽高还受工作区各减 48 物理像素的上限约束并居中 |
| 原生标题栏 | 高（48 DIP），保留系统窗口按钮 |
| 标准状态 | 窗口宽度 ≥ 1140 DIP；导航（196 DIP），检查器（304 DIP） |
| 紧凑状态 | 窗口宽度 < 1140 DIP；导航收为（52 DIP）图标栏，检查器（280 DIP），隐藏页标题下的批次副标题 |
| 页面外边距 | 标准 `24,12,24,20`；紧凑 `16,12,16,16`（DIP，左/上/右/下） |
| 主行间距 | （18 DIP）；页头、搜索、工作面、任务区四行，工作面占剩余高度 |
| 文件列 | 名称/路径占剩余宽度，类型（80 DIP），大小（72 DIP），列间距（12 DIP） |
| 文件行 | 最小高度（64 DIP），内边距 `10,8`，外边距 `4,2`（DIP，水平/垂直）；图标列（28 DIP） |

自适应 `VisualStateManager` 位于页面根 `Grid`，与 `NavigationView` 共用（1140 DIP）切换点。列表关闭水平滚动；右侧两个页签各自纵向滚动。任务区可折叠，展开时占用页面底部高度。目前没有面板拖拽调宽器。

前置 spacing 记录反复出现的间距，并不宣称全部布局严格遵循单一网格。组件 `padding` 采用 XAML 的逗号记法：两个值表示水平/垂直，四个值表示左/上/右/下；全部为 DIP。

## Elevation & Depth

窗口声明原生 `MicaBackdrop`；工作区通过不透明背景、工作面、次级工作面与细边框建立层级。项目没有自定义阴影或悬浮抬升效果；菜单、焦点与控件反馈由原生 Fluent 模板承担。没有自定义 Storyboard、时长或缓动 token，不把加载示例的等待时间当成视觉动效规范。

## Shapes

小标签、文件行、辅助面板与主工作面的圆角分别对应前置 `badge`、`file-row`、`panel`、`work-surface`。检查器外边框仅保留右上和右下圆角，与主工作面拼合。容器边框和分隔线均为（1 DIP）；标准按钮、输入框、菜单和导航的圆角沿用 Fluent 模板，不推导额外的项目默认值。

## Components

| 组件 | 已实现的角色与状态边界 |
| --- | --- |
| 强调按钮 | 原生 `AccentButtonStyle`；空状态的“载入示例文件”可用，参数页“开始复制”禁用。前置按钮 token 仅记录正常填充，文字、悬停、按下、焦点与禁用状态由 Fluent 资源和模板决定。 |
| 普通按钮与菜单 | 原生 Button、DropDownButton、MenuFlyout；主题提供跟随系统/浅色/深色，选择仅对当前运行有效；界面示例入口展示状态和两档窗口尺寸。 |
| 工作区搜索与筛选 | `AutoSuggestBox` 搜索当前内存列表的名称或路径，公共 `SelectionDropDown` 按类型过滤；无文件或加载时禁用。更改搜索或筛选会清除当前选择。独立搜索页使用同一选择控件，见后续补充。 |
| 导航 | `NavigationView` 自动展开/紧凑；工作区和复制整理入口切换右侧页签，任务入口切换队列展开。尚未接入的入口禁用并附说明。 |
| 示例标签 | 原生 Border + TextBlock；有示例文件时显示，使用浅强调背景与强调文字，明确不读取本地文件。 |
| 文件列表 | 原生 `ListView`、Extended 多选与复选框。Ctrl+A 和 Delete 的 `ScopeOwner` 都是 `FileList`；Delete 与“移出列表”只改变内存列表。最近添加到选择的文件显示在检查器；若没有新增选择则使用仍选中的第一项。 |
| 检查器 | 原生 Pivot 分为文件预览和操作参数。预览为类型图标与示例元数据，标记区高（168 DIP），不是文件内容缩略图。目标目录框只读，目录选择、预览复制结果与开始复制均禁用；同名处理与目录结构选项仅供界面切换。 |
| 任务区 | 原生 ToggleButton 控制折叠；展开后显示空队列或固定（62%）的进度示例，并带“未执行文件操作”说明。关闭示例后显示空队列，不表示任务成功。 |

列表区域由 [WorkspaceViewModel](src/FileAssistant.App/Features/Workspace/WorkspaceViewModel.cs) 驱动：无文件为空状态；有文件但过滤后为零项显示无匹配；正常结果显示列表。加载状态显示原生 ProgressRing 与取消按钮，取消保留原批次；错误示例提供重试和返回列表，遮蔽列表但不丢弃已有批次。默认检查器提示选择文件，选中后才显示元数据。具体功能范围不在这里重复定义。

工作区没有新增位图资产，类型预览采用字体图标；包图标与模板栅格资源保持沿用，不能据此推定已完成品牌图标设计。原生组件 metadata 见 [.impeccable/design.json](.impeccable/design.json)；其中不提供 HTML/CSS 仿制预览，也不把 WinUI 资源包装成网页组件。

## 搜索结果表的交互补充（2026-10-30）

搜索页继续采用原生 WinUI、既有浅深色资源和 40 DIP 列表密度。按用户最新纠正，“所有类型”默认展示当前范围文件，清空条件后恢复全部结果；每批 200 项。指定目录在未选择路径时等待，选定路径即显示内容。筛选下方不再显示引擎说明行。

表头的四个列宽分隔使用 12 DIP 命中区域、1 DIP 竖向提示和原生 Thumb 手势，悬停/拖动使用主题强调色及横向调整光标。列宽用共享状态驱动表头和所有虚拟化结果行，支持横向滚动与键盘左右微调；用户调整后，本次运行内窗口变化不覆盖其列宽。

用户最终要求：四列表头及各列内容统一左对齐（包括大小值），纵向居中。搜索范围、类型、大小、日期、排序、工作区类型及同名处理共用 `Controls/SelectionDropDown`。固定按钮下方居中展开原生 Flyout + ListView，选中行采用主题色竖条和 Fluent 浅色背景，不使用圆点或大黑焦点框；方向键浏览时用选中背景和竖条指示当前项。程序清空条件与菜单选中状态同步。类型列表包含可执行文件，并按参考图排列。

所有选择框的按钮、弹层、行高、间距、状态和文字模板集中在 `Resources/SelectionControls.xaml`。选择框中没有硬编码颜色：竖条使用 `SelectionIndicatorBrush`，弹层和边框使用工作面语义画刷，正文、悬停、按下与高对比度使用 Fluent 主题资源。业务选项和对应值分别放在所属功能的 Options 类中，不把搜索逻辑塞进公用控件。维护颜色时修改 ThemePalette；维护选择外观时修改 SelectionControls，现有七处自动复用。

结果上下文菜单使用原生 MenuFlyout 和 FontIcon，单选提供打开及定位，多选提供路径/名称复制与加入批次。右键行与选中状态一致，操作反馈使用既有底部反馈区。此补充只记录本次结果表改动；现有 sidecar 的示例工作区快照未重写。

## Do's and Don'ts

2026-10-30 文件输入补充：暂存区与功能输入共用 `Features/FileInput/FileInputView`，沿用两栏列表/检查器、公共主题和选择框。添加、粘贴、暂存区导入及功能转交为独立动作；导入清单明确选择数量，添加反馈在工作面下方，详情使用可滚动的原生对话框。暂存区移除操作参数标签，功能参数仍留在功能页。标准浅色空状态和选择器入口已观察，完整交互、紧凑/深色及最终标签修复的原生复验待完成，结论为可交接、待验收，见[工作区记录](docs/workspace-design.md)。

2026-10-30 搜索体验补充：底部计数区固定显示匹配、已加载、已选数量和全选范围；旧结果在更新或无效条件时保留并明确标记。列标题使用原生 Button 与 FontIcon 升降序箭头，继续左对齐，分隔处仍由 Thumb 调整宽度。自定义大小/日期入口、单位与常用条件选择复用公共选择框，区间输入按需展开，日期使用原生 CalendarDatePicker，错误在输入附近显示。列宽、排序、信息栏及常用条件的保存不增加新主题、字体或装饰图片；本轮复核与未验证范围见 [搜索设计记录](docs/search-design.md#验证记录)。

这些是复用当前实现时的注意事项，不是用户已确认的永久品牌限制。

### Do:

- Do 使用成对的浅深色 ThemeResource，并保留高对比度资源映射。
- Do 保留原生控件的键盘、焦点、禁用和工具提示行为。
- Do 让长名称可追溯到完整路径，让示例和真实操作结果有明确文字区别。

### Don't:

- Don't 将 DIP 当作物理像素，或把 XamlAutoFontFamily 替换成臆定的网页字体栈。
- Don't 将禁用的真实文件操作、类型图标预览或固定进度示例描述为已完成的业务能力。
- Don't 把本次青色方案、暂用名或四区布局写成用户已确认的永久品牌要求。
