---
name: "FileAssistant（暂用名）"
description: "当前 WinUI 主工作区的原生控件、主题与布局记录"
colors:
  workspace-accent-light: "#242728"
  accent-fill-secondary-light: "#3B403D"
  accent-fill-tertiary-light: "#171B18"
  workspace-accent-subtle-light: "#ECEEE8"
  selection-hover-light: "#E4E7DF"
  workspace-background-light: "#F2F3EF"
  workspace-surface-light: "#FFFFFF"
  workspace-subtle-light: "#FAFAF8"
  workspace-border-light: "#E2E5DD"
  workspace-secondary-light: "#62685F"
  workspace-success-light: "#18703F"
  workspace-text-light: "#252627"
  workspace-on-accent-light: "#FFFFFF"
  workspace-accent-dark: "#D9E1D4"
  accent-fill-secondary-dark: "#E3EADF"
  accent-fill-tertiary-dark: "#C8D2C3"
  workspace-accent-subtle-dark: "#363E36"
  selection-hover-dark: "#414A41"
  workspace-background-dark: "#1C201E"
  workspace-surface-dark: "#252A27"
  workspace-subtle-dark: "#212623"
  workspace-border-dark: "#3D453F"
  workspace-secondary-dark: "#B5BDB4"
  workspace-success-dark: "#8CD8AC"
  workspace-text-dark: "#EEF1EB"
  workspace-on-accent-dark: "#202421"
  system-accent: "#242728"
  system-accent-light1: "#AEB8A9"
  system-accent-light2: "#D9E1D4"
  system-accent-light3: "#ECEEE8"
  system-accent-dark1: "#242728"
  system-accent-dark2: "#1E221F"
  system-accent-dark3: "#151916"
typography:
  headline:
    fontFamily: "XamlAutoFontFamily"
    fontSize: "22 DIP"
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
  work-surface: "9 DIP"
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

## 当前方向（2026-11-02）

用户确认采用本地 `mono-nav-sidebars` 预览并实施到正式 WinUI 界面。横向顶部导航以浅填充表示当前功能，主操作使用炭黑；工作面为白色、次级面为接近白色的中性灰，外围保留少量灰绿。深色采用对应的深灰工作面和浅色主操作。系统字体、细边框与克制圆角维持桌面文件工具的清楚层级。

确认源为 `artifacts/design-concepts-20261102/mono-nav-sidebars/文件操作助手-布局调整预览.html`，该 HTML 是设计参照，正式运行界面仍为 WinUI。产品范围见 [PRODUCT.md](PRODUCT.md)，实施和验收见 [主工作区设计](docs/workspace-design.md)。以下尺寸均为 DIP。

## 公共配色入口

修改颜色只编辑 [ThemePalette.xaml](src/FileAssistant.App/Resources/ThemePalette.xaml) 中对应主题的语义颜色及同文件的系统强调色阶。不要把颜色复制到页面、控件代码或 `App.xaml`。其中 Light、Dark、HighContrast 必须保持相同资源键。

| 颜色键 | 浅色 / 深色 | 用途 |
| --- | --- | --- |
| `AppBackgroundColor` | `#F2F3EF` / `#1C201E` | 页面背景 |
| `AppSurfaceColor` | `#FFFFFF` / `#252A27` | 文件工作面、导航、任务浮层 |
| `AppSubtleColor` | `#FAFAF8` / `#212623` | 次级工作面、列表表头 |
| `AppBorderColor` | `#E2E5DD` / `#3D453F` | 分区边框与分隔线 |
| `AppTextColor` | `#252627` / `#EEF1EB` | 主文字及标题栏前景 |
| `AppSecondaryTextColor` | `#62685F` / `#B5BDB4` | 路径、计数与说明 |
| `AppAccentColor` | `#242728` / `#D9E1D4` | 主按钮、选中指示、进度 |
| `AppOnAccentColor` | `#FFFFFF` / `#202421` | 主按钮中的文字与图标 |
| `AppAccentSubtleColor` | `#ECEEE8` / `#363E36` | 选中导航和文件项 |
| `AppSelectionHoverColor` | `#E4E7DF` / `#414A41` | 选中项悬停/按下 |

`AppAccentSecondaryColor` 和 `AppAccentTertiaryColor` 分别用于主操作悬停和按下；成功提示使用 `AppSuccessColor`。警告、错误、禁用和焦点沿用 Fluent 系统语义资源。

[WorkspaceTheme.xaml](src/FileAssistant.App/Resources/WorkspaceTheme.xaml) 将上述颜色映射为 `Workspace*Brush`，并覆盖 Fluent 的强调填充、强调上的文字、正文与选中行资源。高对比度使用系统窗口色、文字色和 Highlight/HighlightText 配对，不另定义固定黑白。标题栏按钮读取同一主题前景，不再单独硬编码浅深色。

[WorkspaceControls.xaml](src/FileAssistant.App/Resources/WorkspaceControls.xaml) 集中导航选中态、图标按钮、工作面、文件夹行和缩略图项样式。[SelectionControls.xaml](src/FileAssistant.App/Resources/SelectionControls.xaml) 继续统一筛选、单选和动作菜单；选择框竖条与选中背景也随公共主题改变。`App.xaml` 只合并这些字典。本轮未增加用户自定义配色设置页。

## 布局与排版

- 顶部：32 DIP 原生标题栏，随后是功能导航、文件输出区、任务队列、主题与设置。当前导航用 6 DIP 圆角填充，不用下划线。窄窗口隐藏品牌及部分工具文字，图标始终保留可访问名称。
- 输入页：外边距 `24,18,24,12`，页头 22 DIP 半粗标题与计数同行，添加按钮位于右侧。主工作面与设置栏之间 20 DIP 间距。
- 文件夹侧栏：展开 176 DIP，收起 48 DIP；收起与返回上级按钮位于栏头，收起后在窄栏保留恢复与上级入口。树按层级显示目录和数量，通过右键新建，不设常驻新建按钮。
- 文件工作面：默认真实缩略图，支持切换原生 ListView 表格；两种视图共用多选、拖放、键盘与文件夹操作。缩略图宽度按可用空间分列，表格列宽仍可拖动。窄工作面将工具栏分行以避免挤压。
- 工具栏采用 IMG B“一体搜索栏”：目录图标/名称/数量、公共类型筛选与搜索组合、视图与操作依次排列。中央宽度低于 760 DIP 时搜索移至第二行，低于 520 DIP 时操作再分行；搜索文字上下居中、保持靠左。
- 右侧栏：默认 304 DIP，可拖动并保留本次宽度；恢复入口留在 48 DIP 窄栏内。文件区域和右侧栏独立带边框与圆角。列表底部仍可拖动调整高度，双击恢复填满。
- 转换栏：文件预览、输出格式、质量/尺寸、默认展开的转换计划与保存路径依次排列。模式、码率、帧页、表格参数及格式说明收进“更多参数与说明”；内容可滚动，开始转换和实际范围固定在底部。
- 计划随输入/选择/参数变化自动失效并防抖刷新；不会自动执行转换。点击计划标题收起/展开内容，没有底部独立预览按钮。异常输入、同名输出、计划过期和实际执行校验继续由现有服务负责。

## 共享组件与边界

`ResizablePane` 提供主内容、右侧内容、收起后的内容、间距和最小主区宽度；拖动仍复用 `ResizeHandle`。`FileThumbnail` 只对实际生成的文件项请求本地预览，复用本地解码限制与缓存，失败保留文件类型图标。`FilePreviewView.PreviewHeight` 支持转换栏的紧凑预览，其余页面保留原高度。

暂存区、复制输入与转换输入仍独立维护引用。文件输出入口保留真实成功产物，队列通过顶部浮层查看。未完成的复制执行、创建改名等能力继续明确禁用。未增加图片装饰资产；缩略图来自用户本地文件，应用小标记使用简单四格路径，图标资源继续复用系统字体和既有模板。

验收记录及尚未验证的边界见 [主工作区设计](docs/workspace-design.md)。下面保留此前搜索与通用控件说明，其中历史页面结构以本节为准。

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
