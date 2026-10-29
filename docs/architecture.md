# 代码架构与目录规范

> 整理日期：2026-10-29  
> 状态：基于 C# / .NET 10 / WinUI 3 的工程架构约定。  
> 已通过官方模板生成解决方案及三个源码项目，并完成基础构建与非打包启动验证；业务功能尚未实现。  
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

下面展示完整的代码摆放约定。解决方案、三个源码项目及基础配置已生成；空目录使用 `.gitkeep` 保留，测试项目和业务源码按功能逐步添加。当前可运行的模板页面位于 `Features/Workspace/`，落地状态见 [README](../README.md)。

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
│  │  │  ├─ Create/
│  │  │  ├─ Rename/
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
│  │  │  ├─ Rename/
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

- `src` 只放源码和构建所需资源；测试程序放 `tests`。
- 已有产品方案保留在根目录，后续架构和专项说明放 `docs`，避免复制多份同一规范。
- NuGet 依赖通过包管理还原，不手工把 DLL 散落到源码目录。
- 独立转换引擎通过固定版本的获取与打包流程管理；其许可证和版本信息放 `third-party`。
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

## 4. 按功能摆放的示例：批量改名

```text
src/FileAssistant.App/Features/Rename/
├─ RenamePage.xaml                   # 页面布局与数据绑定
├─ RenamePage.xaml.cs                # 必要的界面事件与视图行为
├─ RenameViewModel.cs                # 输入状态、命令和结果展示
└─ RenameItemViewModel.cs            # 需要时添加的逐项展示状态

src/FileAssistant.Core/Features/Rename/
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

tests/FileAssistant.Core.Tests/Features/Rename/
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

目前已使用官方模板建立真实可构建的三个源码项目、解决方案及统一配置，并验证模板窗口和 MVVM 命令绑定。模板示例页面与 ViewModel 已移入 `Features/Workspace`，类库中的空 `Class1` 已移除。测试目录暂时预留，不把空断言当成验证结果。

下一步围绕文件列表、预览和第一条复制流程添加实际代码与对应测试。目录占位不表示功能已经完成；源码与接口随实现添加，不一次性生成所有功能的空页面、空服务和空测试。

完整功能范围见[产品与技术方案](../文件操作助手-产品与技术方案.md)，用户要求和最新纠正见[项目约定](../AGENTS.md)。

