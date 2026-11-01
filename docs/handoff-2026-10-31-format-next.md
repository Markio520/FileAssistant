# 格式转换阶段交接

实现日期：2026-10-31；交接更新：2026-11-01（Asia/Shanghai）。保留开始时已有的主输入列表选择、双击修复及其文档修改。最新能力与面向用户的限制以 [支持格式表](conversion-formats.md) 为准，旧交接仅保留历史记录。

## 2026-11-01 提交与后续安排

用户随后明确要求提交 GitHub。本次提交一并保存格式转换实现、真实文件转换测试、支持格式和许可证说明，以及此前未提交的主输入列表选择/双击修复；此前记录中的“未提交”是当时状态。本次授权不形成永久自动推送规则。

代码仓库仅保存源码、测试、脚本和文档。构建输出、引擎下载缓存、MSIX、测试日志和本机清理清单位于忽略目录，不作为源码提交；下文保留可复现命令及实际验证结果。原生界面、严格断网和正式分发验收仍未完成，不因提交而视为通过。

用户决定将体积优化留到正式打包时处理。本机当前 Debug 主 MSIX 为 152.2 MiB，配套 x64 Windows App Runtime 包为 47.9 MiB，非打包运行目录为 449.8 MiB；可选 LibreOffice 下载为 356.0 MiB，完整开发解包目录实测约 1.49 GiB。这些是当前开发产物数据，不是正式 Release 的大小承诺。本次没有调整引擎分层或删减运行库；正式打包时再核查未使用组件、按架构交付、运行库部署方式和功能回归。

## 实现与运行入口

- 表格：XLS/XLSX/ODS 互转、PDF；CSV/TSV 显式编码、分隔符、公式/计算结果、公式导入开关。多表默认全部输出，每表一个编号文件，计划显示表名和序号；无效表号拒绝执行。
- 演示：PPT/PPTX/ODP 互转和静态 PDF；界面提示字体替代、动画、转场及嵌入音视频的限制。
- 音视频：视频提取 MP3/WAV/M4A/FLAC；计划探测音轨，支持选择序号、适用码率及采样率，无音轨或越界明确报错。原音视频路线保留。
- 图像：自包含 SVG 栅格化、PNG/JPG 多尺寸 ICO、GIF/动态 WebP 全帧互转、多页 TIFF 逐页导出/合并 PDF、明确静态帧号。新增同格式压缩/兼容模式及三种默认预设，普通模式仍取不同目标格式交集。
- PDF：PdfPig 提取 UTF-8 文本，按页序和阅读顺序组织；混合扫描页标记，无有效文字整项失败，不把空 TXT 当作成功。
- 公共流程：沿用现有任务、输出和软件文件夹范围展开；所有参数变化使计划失效；保留原件、重名编号、逐项失败、取消后已成功输出、计划后冲突保护。

用户正常关闭旧进程后，已更新原非打包目录，无需迁移集合数据：

```text
src/FileAssistant.App/bin/unpackaged/x64/Debug/net10.0-windows10.0.26100.0/win-x64/FileAssistant.App.exe
```

可运行 `scripts/run-next-stage.ps1` 直接启动该构建；它在已有实例时拒绝启动，要求先正常关闭以保存列表。需重新构建时使用 `scripts/run.ps1 -Unpackaged`。本轮未通过受阻的界面工具强制关闭进程，也未自动启动第二实例。

主 DLL SHA-256：`C2D75E68976A6250B19CBA90C94571FEE25A2C045F6DB9873D1BA5B611FBB5DC`。完整 EXE/DLL/PRI 哈希在忽略目录 `artifacts/format-next/binary-hashes.json`；EXE 是 .NET 启动器，主 DLL 和 PRI 的变化才反映本阶段功能更新。

## 代码入口与重要取舍

| 层 | 入口 / 职责 |
| --- | --- |
| Core | `Engines/ConversionSettings.cs`、`ConversionCapabilities.cs`、`ConversionPlanner.cs`：参数快照、真实路由、逐表/逐页输出及同格式模式 |
| Infrastructure | `Engines/LocalFileProcessingService.cs`：计划探测、调度、源文件检查、临时输出提交与失败报告 |
| Office | `Engines/Documents/LibreOfficeEngine.cs`：私有源副本和用户配置、Calc/Impress 过滤器、CSV 的 15 个显式参数 |
| 图片 | `Engines/Images/MagickImageEngine.cs`、`WebpAnimationWriter.cs`：逐帧处理、透明画布、ICO 和 SVG 限制 |
| 音视频 / PDF | `Engines/Media/FfmpegConverter.cs`、`Engines/Pdf/PdfPigEngine.cs`：流选择、实际重新编码、PDF 阅读顺序 |
| App | `Features/Convert/ConversionViewModel.Options.cs`、`ConversionOptionsView.xaml`：复用公共选择控件，按适用类型显示参数，任何修改重新规划 |

原生 WebP 动画优化会合并重复帧或裁掉透明画布。本轮复用 Magick 的逐帧无损编码，按公开 RIFF 规范封装完整画布 ANMF；没有新编解码库。GIF 的颜色、透明与时长精度仍受格式自身限制。

LibreOffice 继续作为独立进程使用，保留独立源副本、禁用宏/更新检查等措施；修正 Calc 的“从不更新链接”枚举值，并设置 `BlockUntrustedRefererLinks`。CSV 的默认导入不执行等号公式，主动开启公式时也不自动获取外部 WEBSERVICE 数据。复杂函数、外链和 Excel 特性不承诺完整等价。

图片展开最多 10000 帧且总画布不超过一亿像素；预览缓存上限 200 项/256 MiB。转换不依赖预览缓存。额外引擎仍由用户主动安装，没有新增运行时 NuGet 包或引擎版本。

## 测试与构建记录

所有真实转换均使用测试创建的隔离样本，未拿用户列表中的真实文件执行转换。去重后的本轮测试合计 **280 项通过、0 失败、0 跳过**。最终结果：

| 检查 | 结果 | 本机日志 |
| --- | --- | --- |
| `scripts/test.ps1` | 238 通过，0 失败：Core 75、Infrastructure 65、App 98 | `artifacts/conversion-regression-final.log` |
| `scripts/test-everything.ps1` | 17 通过，0 失败；独立测试实例 | `artifacts/conversion-everything-final.log` |
| `scripts/test.ps1 -Office` | 25 通过，0 失败：原有 8 项、新增 17 项；31 分 53 秒 | `artifacts/conversion-office-final.log` |
| `scripts/build.ps1 -Configuration Debug -Architecture x64` 完整解决方案/打包模式 | 0 警告、0 错误 | `artifacts/conversion-solution-build-final.log` |
| Debug/x64 非打包、自包含 App 构建（原入口） | 0 警告、0 错误 | `artifacts/conversion-unpackaged-primary-final.log` |
| Debug/x64 未签名 MSIX 生成 | 0 错误、1 警告：缺少 `mspdbcmf.exe`，未生成符号包 | `artifacts/conversion-msix-final.log` |
| 物理断网验收 | 未运行；联网检查明确拒绝 | `artifacts/conversion-offline-check.log` |
| 原生 WinUI 鼠标/布局验收 | 受工具权限限制，未验证 | 见下节 |

收尾复核统一了“均衡”预设的初始/恢复图片质量为 90，随后再次运行 App 98 项测试全部通过（`artifacts/conversion-app-presets-final.log`），并重新完成表中的两种构建。Core 和 Infrastructure 未因这项界面默认值调整而变化。

实际 Debug 包位于 `artifacts/format-next/msix/FileAssistant.App_1.0.0.0_x64_Debug_Test/FileAssistant.App_1.0.0.0_x64_Debug.msix`，159,626,404 字节；SHA-256 为 `26E89173709E36F3402678898E209D29128D43E1BA3B74516162AB9FDC5F3BCC`。已直接回读包目录，确认应用、FFmpeg/FFprobe、源码获取说明及 32 项许可材料均在包内（`artifacts/format-next/msix-check.json`）。没有隐藏或关闭符号工具警告；包未签名、未安装，日常运行使用上面的非打包入口。

复现 MSIX 生成命令：

```powershell
. ./scripts/initialize-dotnet.ps1
$taskPackageDirectory = Join-Path (Get-Location).Path 'artifacts/format-next/msix/'
& $taskDotnet build src/FileAssistant.App/FileAssistant.App.csproj --configuration Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxBundle=Never "-p:AppxPackageDir=$taskPackageDirectory"
```

新增真实测试覆盖：

- 六个方向的工作簿转换后再打开，核对两表、中文、SUM 公式、数值、PDF 和原件哈希；UTF-8/GB18030、分号 CSV、TSV、公式/文本导入、指定表、越界及中途取消。
- 六个方向的演示转换后生成并重新读取 PDF，核对两页、720×405 pt 宽屏尺寸、中文和英文、可读字体资源、图片、图表标题及渲染图中的红色图像区域。这是合成样本内容验证，不是复杂商业幻灯片的像素一致性承诺。
- 视频的第二音轨分别提取四种音频，回读编码/采样率并解码波形验证 880 Hz 所选音轨；无音轨错误、越界轨道、MP4 同格式重编码和尺寸。
- SVG 三种输出与外链拒绝、ICO 七个尺寸、动画帧数/重复帧/顺序/时长/有限与无限循环/透明清除、选定静态帧、多页 TIFF 图片与 PDF、JPG 同格式缩小与编号、PDF 文本页序/换行/扫描件拒绝。
- App 参数变更后的计划失效、目标模式切换、计划参数快照；原范围展开/去重、取消、冲突和任务反馈回归测试继续通过。

测试文件主要为 `NextStagePlanningTests.cs`、`NextStageConversionTests.cs`、`OfficeNextStageTests.cs`，及扩充的 `MediaConversionTests.cs`、`ConversionWorkflowTests.cs`。Office 测试使用单独串行集合，避免旧有私有安装测试与转换测试互相干扰。

Office 每次转换使用独立配置；多工作表逐表导出以及 XLS 的工作表探测会反复启动引擎，本机完整往返矩阵耗时较长。批量表格速度没有做大规模性能承诺，仍可按项取消并保留已成功产物。

## 尚未验证与下一次验收

1. **原生界面**：Computer Use 能列出旧窗口，但读取要求激活；激活返回 `GetCursorPos failed: 拒绝访问。(0x80070005)`。未绕过工具改用其他输入手段。需在权限恢复后检查主列表单击、Shift 范围、Ctrl 增减、文件夹双击，以及新参数面板滚动/布局和实际运行入口。现有 `Extended` 原生选择、列表级拖拽和排队双击导航修复已保留在构建中；ViewModel 测试不能替代鼠标验收。
2. **严格离线**：本机仍有联网接口且无网络管理权限。真实测试每例采用新路径、冷缓存和独立 Office 配置，但不能据此称“断网通过”。准备好引擎/测试程序集后正常断网，再运行 `scripts/test.ps1 -Offline` 和 `scripts/test.ps1 -Office -Offline`；此模式禁止 restore/build/下载，联网时拒绝运行。外部 WEBSERVICE 的本机监听测试只验证该访问路径没有请求，不代表系统级网络隔离。
3. **分发审计**：Debug 编译和未签名 MSIX 生成已验证；缺少原生符号包工具，安装、提权、卸载、签名和 Release 未验收，未对外发布。对外分发前仍需核对 FFmpeg/原生依赖完整对应源码、补丁与构建材料；详见 [源码获取说明](../third-party/SOURCE-ACCESS.md)。项目自有代码许可证未确定，未擅自新增 LICENSE。

用户关闭后，`conversion.json`、`copy.json`、`staging.json` 三份 Collections 文件的 SHA-256 与本轮记录前一致。核对记录在 `artifacts/format-next/collections-before.json`、`collections-after.json`；没有覆盖恢复文件或改变用户层级。

## 清理与缓存

测试正常完成时自行清理各自 GUID 样本和转换私有目录；保留现用 LibreOffice、FFmpeg 开发缓存以避免重复下载。新建临时测试构建 `artifacts/format-next/probe-tests/` 为 119 文件、279,105,638 字节（约 266 MiB）。删除前校验了绝对路径和重解析点，但执行工具在进程启动前返回 `blocked by policy`，目录仍在，未绕过限制。返回信息没有说明具体命中规则，不能据此归因为 Auto-review 自动审批审核。

2026-10-31 交付时的残留清单记录在 `artifacts/format-next/cleanup.json`：上述测试构建、已被原入口取代的独立构建 `artifacts/format-next/unpackaged/`，以及中断诊断运行留下的 4 个 GUID 临时目录，当时共 **6 个目录、1009 个文件、751,700,789 字节（约 717 MiB）**。后四个目录位于系统 Temp，前缀为 `FileAssistant-convert-`、`FileAssistant-office-next-`，完整路径在清单中。实际被拒绝的目标只有 `probe-tests`，其他五个目录当时没有提交删除。

2026-11-01 删除诊断：对 `probe-tests` 使用 `Remove-Item -Recurse -WhatIf` 的无修改预演成功；另新建一个 40 字节的独立临时文件，使用不带 `-Recurse` 或 `-Force` 的 `Remove-Item -LiteralPath` 删除成功，随后 `Test-Path` 返回 `False`。测试文件未残留。这证明本机会话并非禁止所有删除，但没有确定此前拒绝的具体规则。

2026-11-01 用户再次明确要求清理后，正常删除了此前未提交删除的五个独立目标：`artifacts/format-next/unpackaged/` 和上述四个 Temp 目录。删除前核对绝对路径、全部后代重解析点、文件数量和字节数，均与原清单一致；使用原生 PowerShell `Remove-Item -LiteralPath -Recurse`，未使用 `-Force`。五个目录删除后 `Test-Path` 均返回 `False`，合计清理 **890 个文件、472,595,151 字节（约 451 MiB）**。当前只剩 `probe-tests`：**119 个文件、279,105,638 字节（约 266 MiB）**；此前拒绝的目标未重试，也未拆分或换用工具删除。

最终 25 项 Office 测试的自身清理完成，未遗留其活动进程；上述 4 个 Temp 目录来自更早的中断诊断运行，已在后续清理中删除。`probe-tests` 留待策略允许时清理，不要删除原运行目录、现用引擎、Debug MSIX 交付物或用户集合。此前 `artifacts/input-selection-qa/` 属于历史交接，不计作本轮已清理。

本阶段未包含 OCR、PDF 转可编辑 Word/Excel、专业 RAW/CAD/3D 和压缩包转换。
