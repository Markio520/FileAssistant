# 代码架构与目录规范

> 整理日期：2026-10-29  
> 状态：基于 C# / .NET 10 / WinUI 3 的工程架构约定。  
> 当前已建立三个源码项目，接入真实文件搜索、共享引用输入、真实预览和常用本地格式转换；复制与改名执行尚未实现。最新验证边界见开发交接。
> FileAssistant 是示例代码前缀，不代表产品名称已经确定。

## 1. 架构选择

采用一个 .NET 解决方案、三个源码项目及相应测试项目。界面使用 MVVM，项目内部按功能组织代码。

| 项目 | 主要职责 | 不承担的职责 |
| --- | --- | --- |
| FileAssistant.App | WinUI 窗口、页面、ViewModel、控件、样式、资源和应用启动组装。 | 页面和 ViewModel 不直接执行文件修改、SQL 或调用转换引擎。 |
| FileAssistant.Core | 业务模型、处理规则、操作计划、任务编排、方案及恢复条件；定义所需外部能力接口。 | 不引用 WinUI、SQLite、Magick.NET、7-Zip 等具体实现，不直接调用系统文件 API。 |
| FileAssistant.Infrastructure | 实现文件读写、Windows 集成、SQLite 持久化，以及图片、PDF、压缩等组件适配。 | 不组织页面和视觉状态，不决定用户如何选择处理规则。 |

Core 内部通过文件夹区分规则与流程，初期不再拆分独立 Domain、Application 项目。接口用于隔离文件系统、持久化和外部组件等真实边界，普通规则类不逐个制造接口。

## 2. 仓库目录

下面展示完整的代码摆放约定。解决方案、三个源码项目及基础配置已生成；空目录使用 `.gitkeep` 保留，测试项目和业务源码按功能逐步添加。当前可运行的主工作区位于 `Features/Workspace/`，落地状态见 [README](../README.md)。

```text
FileAssistant/
├─ FileAssistant.slnx                 # 整个解决方案入口
├─ global.json                        # 锁定已验证的 .NET SDK
├─ Directory.Build.props              # 统一编译与代码分析选项
├─ Directory.Packages.props           # 集中管理 NuGet 依赖版本
├─ .editorconfig                      # 缩进、命名、格式等规范
├─ .gitignore                         # 排除构建产物和本机文件
├─ README.md                          # 环境、构建、运行、测试入口
├─ AGENTS.md                          # 项目协作与实现约定
├─ 文件操作助手-产品与技术方案.md       # 已有的产品方案，保留当前位置
│
├─ src/
│  ├─ FileAssistant.App/
│  │  ├─ FileAssistant.App.csproj
│  │  ├─ App.xaml
│  │  ├─ App.xaml.cs
│  │  ├─ MainWindow.xaml
│  │  ├─ MainWindow.xaml.cs
│  │  ├─ Package.appxmanifest         # 若采用单项目 MSIX，清单放这里
│  │  ├─ Bootstrap/                   # 启动、依赖注入与服务组装
│  │  ├─ Features/                    # 按功能放页面和 ViewModel
│  │  │  ├─ Workspace/               # 已有 WorkspacePage 和 WorkspaceViewModel
│  │  │  ├─ Search/
│  │  │  ├─ Preview/
│  │  │  ├─ CreateOrRename/           # 批量创建与改名，共用一个入口
│  │  │  ├─ Organize/
│  │  │  ├─ Convert/
│  │  │  ├─ Archive/
│  │  │  ├─ Verify/
│  │  │  ├─ Cleanup/
│  │  │  ├─ Tasks/
│  │  │  └─ Settings/
│  │  ├─ Controls/                    # 跨页面复用的界面控件
│  │  ├─ Resources/                   # 主题、样式、颜色与尺寸资源
│  │  ├─ Assets/                      # 图标、图片等静态资源
│  │  ├─ Strings/                     # 界面文案与本地化资源
│  │  └─ Properties/                  # 项目所需的运行与发布属性
│  │
│  ├─ FileAssistant.Core/
│  │  ├─ FileAssistant.Core.csproj
│  │  ├─ Models/                      # 多个业务功能共用的模型
│  │  ├─ Abstractions/                # 外部能力接口
│  │  ├─ Features/                    # 各功能的选项、规则和计划生成
│  │  │  ├─ Search/
│  │  │  ├─ CreateOrRename/
│  │  │  └─ ...                       # 其余功能在实现时添加
│  │  ├─ Operations/                  # 操作计划、执行编排、结果与恢复检查
│  │  ├─ Jobs/                        # 队列、取消、进度与重试策略
│  │  └─ Presets/                     # 保存方案的业务模型与流程
│  │
│  └─ FileAssistant.Infrastructure/
│     ├─ FileAssistant.Infrastructure.csproj
│     ├─ FileSystem/                  # 基于系统 API 的文件访问实现
│     ├─ Windows/                     # 回收站、Shell、属性和平台互操作
│     ├─ Archives/                    # 7-Zip 等压缩组件适配
│     ├─ Images/                      # Magick.NET 等图片组件适配
│     ├─ Pdf/                         # PDF 组件适配
│     ├─ Media/                       # 媒体信息和预览数据适配
│     ├─ Persistence/                 # SQLite、迁移与 JSON 存储实现
│     └─ Processes/                   # 外部引擎进程的调用与输出处理
│
├─ tests/
│  ├─ FileAssistant.Core.Tests/       # 规则、计划和流程单元测试
│  ├─ FileAssistant.Infrastructure.Tests/ # 真实文件与组件集成测试
│  └─ FileAssistant.App.Tests/        # 有可运行界面后按需加入 UI 测试
│
├─ docs/
│  └─ architecture.md                # 本文；其他开发文档按需添加
├─ scripts/                          # 构建、测试、打包与开发辅助脚本
├─ packaging/                        # 发布配置、安装资源等；不存安装包产物
├─ third-party/                      # 组件清单、许可证与第三方声明
└─ artifacts/                        # 安装包、测试报告等产物，不提交 Git
```

### 目录职责补充

- 软件文件夹由 Core `Features/FileCollections` 维护树、成员和可移植转交包，真实磁盘文件夹保留 `SearchFile.IsFolder` 语义。`WorkspaceViewModel.Collections` 维护独立输入实例的范围、导航及自动保存，`FileInputView.Collections` 与共用文件夹选择/来源选择控件负责交互。Infrastructure `JsonFileCollectionStore` 执行分实例原子保存和失效引用恢复；页面不直接读写保存文件。详细规则见 [软件文件夹设计](file-collections-design.md)。
- 输入整理的单步撤销集中在 `WorkspaceViewModel.Undo`，快照只影响该输入实例；恢复后仍走统一通知、计划失效及自动保存流程，不持久化撤销历史。`CollectionSourceSelection` 管理导入弹窗的跨目录选择，与来源页本身的选择分离；来源、目标、层级选项统一编排在 `FileCollectionDialogs`，不由页面串联多个选择弹窗。

- 常见转换共用原有计划、原件校验、临时输出与原子提交；`Infrastructure/Engines/Documents/LibreOfficeEngine` 隔离用户文件和 Office 配置，`Media/FfmpegConverter` 探测实际流并按固定容器/编码组合转换，`LocalEngineProcess` 统一参数列表、无界面启动、超时与取消。`EnginePackageInstaller` 支持已校验 ZIP 及官方 MSI 的私有行政解包，原子切换安装指针；设置负责用户触发的按需安装，成功后刷新转换能力，不自动开始任务。

- 转换能力表在 Core `Engines/ConversionCapabilities`：共享图片输入类型，按文件方向计算输出交集；转换 ViewModel 只把真实能力映射成动态 `SelectionOption`，值保持为 `ConversionTarget`。`WorkspaceViewModel.SetSelection` 提供完整选择快照，转换按“已选优先、无选择则全部”生成同一批次的格式菜单、预览和执行计划，PDF 保持列表顺序。选中项变化和过期计划由 ViewModel 隔离，不在页面中复制格式规则。

- 表格列宽和右侧栏共用 `Controls/ResizeHandle` 的原生 Thumb、左右键与横向光标，外观统一放在 `Resources/ResizeControls.xaml`，网格宽度转换器位于 `Controls/ColumnWidthConverter`。`Controls/ResizablePane` 负责右侧栏的宽度边界、收起及恢复，窗口缩小时限制实际宽度但保留用户偏好。搜索列宽仍由 `SearchColumnWidths` 管理并沿用持久化；输入表的三列由每个 `FileInputView` 独有的 `FileInputColumnWidths` 管理，表头和行共享绑定，横向滚动同步。暂存区、复制、转换通过共用输入视图接入，输出区也复用同一侧栏容器。侧栏宽度与新增输入列宽本轮仅保存在当前页面实例中。

- 单选下拉的公共行为放在 `App/Controls/SelectionDropDown.cs`，选项类型 `SelectionOption` 不依赖 WinUI。搜索与工作区的类型选项共用 `Features/FileInput/FileTypeFilterOptions`，过滤均复用 Core 的 `SearchRules`；其余业务选项分别放在 `SearchFilterOptions`、`WorkspaceSelectionOptions`。页面只绑定稳定的选项值，不用菜单顺序或字符串 Tag 推断业务值。
- 两个页面的外观入口统一使用 `Controls/ThemeSelectionDropDown`，继承公共选择框的弹层与选中样式，保留图标按钮。控件订阅应用根节点的 `RequestedTheme` 并在卸载时取消订阅，确保两处选中状态同步；主题切换不再由页面菜单事件分别处理。
- 所有选择框复用 `Resources/SelectionControls.xaml` 的按钮、弹层、选中行和间距。`ThemePalette.xaml` 集中维护配色，`WorkspaceTheme.xaml` 将颜色映射为语义画刷；控件通过 `ThemeResource` 消费，不保存颜色副本。保留 Fluent 的文字、交互状态与高对比度资源。
- `SelectionDropDown` 用原生 DropDownButton、Flyout、ListView 与 ListViewItemPresenter 组合：按钮文本固定、菜单下方居中，单选竖条、键盘导航和焦点由公共控件管理。方向键浏览，回车/空格或点击确认，Esc/点击外部不提交；外部清空条件时按钮和下次打开的选中项同步。
- 添加、用于功能、界面示例等动作入口使用 `Controls/ActionDropDown`，复用同一资源字典中的按钮、弹层间距、下方居中位置和语义配色。文本动作项共用 `ActionDropDownItemStyle`，悬停及键盘焦点只显示背景，不显示竖条或黑色焦点框。绿色竖条仅表示筛选菜单的已选项，不用于未选项悬停；动作仍使用原生 `MenuFlyout`，保留命令、禁用状态与键盘行为，不记录单选值。
- 用户指定搜索页类型菜单作为统一高度标准：按钮/菜单行最小高度 32 DIP，行内边距 12,6、外边距 0,1，正常字体下相邻行间距 34 DIP；弹层内边距 4 DIP，竖条 3×16 DIP。公共尺寸保存在 `SelectionControls.xaml`，页面不覆盖行高；字体缩放时允许内容自然增高。
- 含编译绑定的 `SelectionControls` 资源字典必须在 `App.xaml` 中通过 `<resources:SelectionControls />` 实例化，不能改成仅设置文件 `Source`，否则模板文字的绑定初始化不会运行。参见 [Microsoft：资源字典中的 x:Bind](https://learn.microsoft.com/en-us/windows/uwp/data-binding/data-binding-in-depth#resource-dictionaries-with-xbind)。

- `src` 只放源码和构建所需资源；测试程序放 `tests`。
- 已有产品方案保留在根目录，后续架构和专项说明放 `docs`，避免复制多份同一规范。
- NuGet 依赖通过包管理还原，不手工把 DLL 散落到源码目录。
- 独立转换引擎按基础/扩展能力分层：基础包走固定版本打包，扩展包由用户在设置中按需下载，匹配架构并校验完整性后安装到应用数据目录。运行中任务固定使用已选引擎版本；引擎管理与文件处理分开，下载完成不自动开始转换。其许可证和版本信息放 `third-party`，按需下载不替代许可核对。当前引擎管理支持 FFmpeg 修复及 LibreOffice 按需包，细节见 `engines/README.md`。
- `bin`、`obj`、`.vs`、`TestResults` 和 `artifacts` 等构建或本机输出纳入忽略规则。
- 日志、缓存、数据库、下载的引擎使用 Windows 应用数据目录；用户处理结果使用用户选择的位置，不写进源码或安装目录。
- MSIX 清单只维护一处。`packaging` 保存额外发布配置，实际安装包输出到 `artifacts`。
- `README.md` 提供统一构建与测试入口；其他开发者应能通过文档和已声明的依赖搭建环境。

## 3. 项目依赖方向

以下箭头表示项目引用，不表示运行时调用顺序。

```mermaid
flowchart LR
    App["App：界面与启动"] --> Core["Core：规则、流程与接口"]
    App -->|"仅启动组装处使用具体实现"| Infra["Infrastructure：系统与组件适配"]
    Infra --> Core
```

执行时遵循：

- App 的页面与 ViewModel 调用 Core 的业务能力。
- Core 通过接口请求文件访问、记录保存或转换能力。
- Infrastructure 实现这些接口，在 App 的启动位置统一注册。
- App 对 Infrastructure 的引用用于启动组装；页面不直接创建数据库、文件访问器或第三方引擎。
- Core 不反向引用 App 或 Infrastructure，Infrastructure 不引用 App。
- 第三方组件类型、数据库连接和 WinUI 控件不穿过 Core 的接口；跨边界传递业务模型、参数和结果。

Core 使用普通 `net10.0` 目标，App 和 Infrastructure 使用 `net10.0-windows10.0.26100.0`。工程最低系统版本参数目前沿用模板的 `10.0.17763.0`，实际仅在 Windows 11 x64 验证，不将该参数视作对所有旧系统的兼容性承诺。组件版本集中记录在 `Directory.Packages.props`。

## 4. 按功能摆放的示例：批量创建与改名

2026-10-30 用户确认合并入口，App 和 Core 的预留目录统一为 `Features/CreateOrRename`。以下以其中的改名规则展示职责划分；创建与改名仍按各自规则生成计划，不把合并入口等同于实际处理功能已实现。

```text
src/FileAssistant.App/Features/CreateOrRename/
├─ CreateOrRenamePage.xaml           # 合并入口的页面布局与数据绑定
├─ CreateOrRenamePage.xaml.cs        # 必要的界面事件与视图行为
├─ CreateOrRenameViewModel.cs        # 输入状态、命令和结果展示
└─ RenameItemViewModel.cs            # 需要时添加的逐项展示状态

src/FileAssistant.Core/Features/CreateOrRename/
├─ RenameOptions.cs                  # 用户规则对应的业务参数
└─ RenamePlanner.cs                  # 根据输入生成重命名计划

src/FileAssistant.Core/Operations/
├─ OperationPlan.cs                  # 可供预览及执行的计划
└─ OperationExecutor.cs              # 经检查后调用外部能力执行

src/FileAssistant.Core/Abstractions/
├─ IFileAccess.cs                    # 文件访问边界示例
└─ IOperationJournal.cs              # 操作记录边界示例

src/FileAssistant.Infrastructure/FileSystem/
└─ SystemFileAccess.cs               # 系统文件操作实现

src/FileAssistant.Infrastructure/Persistence/
└─ SqliteOperationJournal.cs         # 操作记录持久化实现

tests/FileAssistant.Core.Tests/Features/CreateOrRename/
└─ RenamePlannerTests.cs
```

上述类名是职责示例，实际类只在有具体实现需要时创建。相关页面与 ViewModel 放在同一个功能目录，业务规则和系统实现分别归入对应项目。

从点击按钮到完成操作的流程：

> Page / ViewModel 收集参数 → Core 生成计划 → App 展示预览 → Core 复核并编排执行 → Infrastructure 实际操作文件 → Core 汇总结果 → App 展示结果

页面的 code-behind 可以处理焦点、拖放事件转发、窗口生命周期等视图行为。规则计算、文件修改、数据库访问和转换进程调用不写进 code-behind。

## 5. 代码组织规则

1. **按职责命名。** 主要类型通常独立成文件，文件名与类型名对应；命名空间与项目及目录保持一致。
2. **功能内聚。** 某功能专用的模型、转换器和辅助代码就近放置；只有被多个功能实际共用时才移入公共目录。
3. **共享目录有明确用途。** 不用一个无限增长的 `Utils`、`Helpers` 或 `Common` 收纳无关逻辑。
4. **界面状态与业务模型分开。** 选中、高亮和面板展开等状态属于 App；文件计划、规则和执行结果属于 Core。
5. **系统与第三方调用集中。** 文件 API、Windows 互操作、SQL、压缩与转换调用放在 Infrastructure 的对应目录。
6. **异步边界清楚。** 耗时扫描、校验和转换不阻塞 UI；取消和进度从操作流程传递，界面负责展示。
7. **统一计划和记录。** 预览与执行基于同一份操作计划；执行前检查实际状态，逐项记录部分完成和失败。
8. **依赖按需引入。** 不为每个功能创建单独项目或增加通用框架；只有出现明确隔离或发布需求才调整项目划分。
9. **按职责拆分大类。** 发现一个类承担多个无关职责时拆分，而不是仅为满足固定行数机械切文件。

## 6. 构建与依赖配置

| 文件 | 作用 |
| --- | --- |
| `FileAssistant.slnx` | 唯一解决方案入口，包含源码与需要的测试项目。 |
| `global.json` | 记录已验证的 SDK 版本与适当的版本回退规则。 |
| `Directory.Build.props` | 统一可空引用类型、编译、分析等设置；Windows 专用选项按项目限定。 |
| `Directory.Packages.props` | 集中固定 NuGet 包版本；各项目只声明自己实际需要的包。 |
| `.editorconfig` | 统一代码格式与命名约定。 |
| `.gitignore` | 排除编译、测试输出及机器相关文件。 |

采用 `.slnx` 与 .NET 10 的默认解决方案格式一致；只维护一个解决方案入口。[.NET 解决方案文档](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln)

NuGet 支持通过 `Directory.Packages.props` 集中管理版本；MSBuild 支持通过 `Directory.Build.props` 设置目录下各项目的通用构建属性。[NuGet 集中包管理](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) · [MSBuild 目录配置](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)

当前工程同时维护各源码项目的 `packages.lock.json`。WinUI 打包与非打包模式分别使用 `bin/packaged`、`obj/packaged` 和 `bin/unpackaged`、`obj/unpackaged`，避免切换模式时复用不适用的 PRI 资源索引；源码扫描同时排除整个 `bin`、`obj` 目录。

## 7. 测试摆放与验证

| 测试项目 | 重点 |
| --- | --- |
| Core.Tests | 命名规则、编号、清单映射、冲突计划、取消与重试策略、恢复条件。 |
| Infrastructure.Tests | 在独立临时目录中验证实际文件读写、移动、压缩转换、数据库和错误传播。 |
| App.Tests | 有可运行界面后验证关键交互、拖放、预览、状态反馈；具体 UI 测试方案另行验证。 |

- 测试目录按对应功能或适配器组织，避免全部测试堆在一个文件。
- 真实文件测试使用专门生成的临时数据，不操作用户原始文件；固定样例放在所属测试项目中。
- 更重视规则边界和失败场景，例如重名、文件占用、执行前目标变化、取消和部分完成。
- UI 相关代码的测试需要适合 Windows/WinUI 的运行方式，不假定普通测试进程能直接启动所有控件。
- 项目引用和构建检查应防止 Core 引入 UI 或具体基础设施依赖。

## 8. 初期落地方式

目前已建立真实可构建的三个源码项目、解决方案及统一配置。主工作区位于 `Features/Workspace`，已替换计数器模板；`WorkspacePage` 负责布局与输入适配，`WorkspaceViewModel` 负责列表与展示状态，`FileInspector` 和 `WorkspaceTaskPanel` 分别承载详情/参数和队列。共享主题位于 `Resources/WorkspaceTheme.xaml`。

`WorkspaceFile` 是 App 展示模型，可引用真实的 Core `SearchFile`；`WorkspaceSamples` 仅用于界面示例。加入真实文件会清除示例，完整列表与当前筛选结果分别维护。旧的临时状态检查位于忽略的 `artifacts/workspace-state-check`。

2026-10-30 第三步输入职责：`WorkspacePage` 只编排导航及功能转交；`App.Workspace` 与 `App.CopyInput` 分别持有暂存区和复制功能的独立 `WorkspaceViewModel`。两处复用 `Features/FileInput/FileInputView`，系统选择器、剪贴板与拖放的数据格式适配由同目录 `FileInputSources` 处理。Core `IFileInputReader` / `FileImportReport` 定义读取边界及逐项结果，Infrastructure `FileInputReader` 只规范化路径、检查存在/权限并读取属性，不递归展开、不修改原件。搜索加入、功能导入均重新调用该读取边界；输入中的异步读取支持取消及过期结果隔离。当前任务队列仍共用全局展示状态，不因增加功能输入而复制队列。

搜索第一阶段按如下职责落地：

- Core `Features/Search` 保存范围、条件、结果模型和共用语法规则；`Abstractions/IFileSearch` 是搜索边界。
- Infrastructure `FileSystem` 保存目录快照扫描和搜索分派；`Windows/EverythingSearchClient` 使用官方 QUERY2W IPC，后台线程建立独立消息窗口并处理超时、取消和有界结果解析。
- `Windows/EverythingRuntime` 管理随包引擎的配置、预热、共享准备任务、恢复与关闭；`EverythingDistribution` 固定版本和校验值；`EverythingServiceSetup` 把专用辅助服务安装到受保护目录。App 启动与关闭只负责组装和转交生命周期，短暂提权调用在创建窗口前完成服务准备，不提升日常文件操作界面。
- App `Features/Search` 保存 WinUI 视图、显示模型及输入防抖/过期结果保护。文件夹选择器由视图持有窗口句柄，文件访问均经接口。具体搜索服务只在 `App.OnLaunched` 组装。
- 搜索表格的共享列宽保存在 App `SearchColumnWidths`，行模型与表头使用同一实例；Thumb 手势和上下文菜单定位属于视图行为。Core `IFileInteraction` 隔离打开、定位与复制文本，Infrastructure `WindowsFileInteraction` 调用 Windows Shell 和剪贴板，ViewModel 组织反馈并可独立测试。
- 搜索视图负责离开前记录、返回布局完成后恢复滚动位置；ViewModel 保留已完成的结果与选择，区分替换查询和翻页失败。`SearchViewModel.Filters` 管理自定义条件与表头排序，`SearchViewModel.Preferences` 管理常用条件与合并保存；布局与条件存储模型仍位于 App。Core `ISettingsStore` 仅定义文档读写边界，Infrastructure `JsonSettingsStore` 负责文件存储与完整替换，不引用界面类型。`ThrottledSearchProgress` 在进度进入 UI 调度队列之前限制通知频率。
- Core.Tests 验证共用语法和分页；Infrastructure.Tests 验证真实临时目录和可选 Everything 实例；App.Tests 链接不依赖 WinUI 的状态模型，验证输入竞态、取消、输入法组合和批次独立性。原生控件另用 WinApp UI Automation 检查，不把这些状态测试称为完整 UI 自动化测试。

下一步先按[开发交接](handoff-2026-10-30.md)完成第三步原生输入验收，再按用户确定的顺序接入实际处理功能。目录占位不表示功能已经完成；源码与接口随实现添加，不一次性生成所有功能的空页面、空服务和空测试。

完整功能范围见[产品与技术方案](../文件操作助手-产品与技术方案.md)，用户要求和最新纠正见[项目约定](../AGENTS.md)。


## 本地引擎、转换与输出职责（2026-10-30）

Core 的 Engines 定义预览、转换计划/结果及下载契约；Infrastructure 的 Engines 按 Images、Media、Pdf 隔离 Magick.NET、FFmpeg、PDFium 和 PdfPig，并集中处理文件状态、缓存、无覆盖提交及版本包安装。App 只在启动时组装接口，Features/Preview 显示结果，Features/Convert 配置和执行同一计划，Features/Tasks 管理进度与取消，Features/Outputs 记录真实成功产物并按任务筛选。功能输入与全局暂存区始终独立。

根目录 engines 保存固定下载清单和接入说明；二进制进入忽略的 artifacts 开发缓存或运行目录，引擎与用户输出文件分开。设置只在用户点击时下载，校验与激活完成后供公共服务调用。目录、示例、能力及验证边界见 [本地引擎说明](../engines/README.md)。
