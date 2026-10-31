# 本地处理引擎

预览、转换和后续功能统一通过 `FileAssistant.Core.Engines.IFileProcessingService` 调用，不在页面中直接引用原生库或拼命令行。该目录保存固定版本下载清单和接入说明，大型二进制不提交 Git。

## 目录与调用

- `src/FileAssistant.Core/Engines/`：预览、转换计划、进度和实际输出结果的公共契约；不依赖 WinUI 或第三方引擎。
- `src/FileAssistant.Infrastructure/Engines/Images/`：Magick.NET 图片解码、方向处理、预览和编码。
- `src/FileAssistant.Infrastructure/Engines/Media/`：FFmpeg 首帧提取、音视频探测与格式转换，使用参数列表启动本地进程。
- `src/FileAssistant.Infrastructure/Engines/Documents/`：LibreOffice 文本文档转换，私有源文件副本和独立用户配置，禁用宏、自动更新文档链接和 OpenCL。
- `src/FileAssistant.Infrastructure/Engines/Pdf/`：PDFium 渲染与 PdfPig 建页。
- `EnginePackageCatalog` / `EnginePackageInstaller`：固定清单、用户触发的下载、SHA-256 校验、受限解压与原子激活。
- `src/FileAssistant.App/Features/Preview/`：公共预览控件；`Features/Convert/` 为独立转换输入和参数；`Features/Outputs/` 为真实输出文件记录；`Features/Tasks/` 展示实际进度和逐项结果。

调用顺序：

```csharp
var preview = await processing.PreviewAsync(localPath, 700, cancellationToken);
var plan = await processing.CreatePlanAsync(
    new ConversionRequest(inputPaths, outputDirectory, ConversionTarget.WebP), cancellationToken);
// 将 plan.Items 展示给用户，点击开始后执行同一份计划。
var result = await processing.ConvertAsync(plan, progress, cancellationToken);
// 只将 result.Outputs 加入输出区；失败/取消项留在 result.Items。
```

`PreviewAsync` 返回 PNG 字节或文件开头文本。转换始终重新读取原件，不使用预览缓存作为转换源。已有同名文件自动编号；预览之后才出现的冲突文件不覆盖，输入状态变化则停止该项。临时输出完成后才提交，取消不回滚已生成文件。

2026-10-31：输入类型与已实现目标集中在 Core `ConversionCapabilities`。界面按选中项（无选择时为全部输入）计算目标交集，并隐藏同格式输出；没有共同目标时阻止生成计划，不静默忽略不支持项。格式值是稳定的 `ConversionTarget` 值，与动态菜单位置无关。选中范围和参数改变会使先前计划失效。该规则不会把上游引擎支持的全部格式直接暴露为本软件能力；新能力需完成实际适配和验证后登记。

## 已接入版本与范围

| 引擎 | 固定版本 | 当前能力 |
| --- | --- | --- |
| Magick.NET-Q16-x64 | 15.0.0 | 常用图片预览，PNG/JPEG/WebP/BMP/TIFF/GIF/AVIF 输出；按扩展名显式选择解码器 |
| FFmpeg，LGPL shared x64 | 8.1.3-9-g29e619e767 | 视频首帧；MP3/WAV/FLAC/M4A/AAC/OGG 音频与 MP4/MKV/MOV/WebM 视频输出 |
| LibreOffice，按需 x64 | 26.2.6（26.2.6.3） | DOC/DOCX/ODT/RTF/TXT 互转及每份文档单独 PDF 输出 |
| PDFiumCore / PDFium | 157.0.8100 | 首页预览、按页导出图片 |
| PdfPig | 0.1.16 | 多图按输入顺序生成一个 PDF，一图一页、A4 自适应横竖向 |

图片输入包含 JPG/PNG/WebP/BMP/GIF/TIFF/ICO/AVIF/HEIC/HEIF；多帧图片只读取第一帧，GIF 输出为静态图。七种图片输出均通过实际编码测试；其余图片解码器仍需补充样本。视频输入包含 MP4/MOV/MKV/AVI/WMV/WebM/M4V；音频输入包含 MP3/WAV/FLAC/M4A/AAC/OGG/Opus/WMA/AIFF。PDF 导出 72–600 DPI，图像/页面不超过一亿像素；JPEG/BMP 透明区域铺白。部分文本文件仅预览开头 32 KiB。密码 PDF、文件夹转换、表格/演示文档、PDF 转 Word、交互式播放和多页预览尚未开放。

音频转换首个音轨：MP3 使用 libmp3lame，WAV 为 16 位 PCM，FLAC 为无损编码，M4A/AAC 使用原生 AAC，OGG 使用 Vorbis；有损来源转无损格式不恢复音质。视频转换首个非封面视频流及可选首个音轨，舍弃字幕、其他轨道和原元数据；MP4/MKV/MOV 使用 libopenh264 + AAC，WebM 使用 VP9 + Opus，奇数尺寸补边到偶数。转换后重新检查原件状态再原子提交；取消终止本任务进程树，临时输出不进入输出区。

文档按单份输出，不合并 PDF；TXT 按 UTF-8 导入/输出，TXT 导出丢弃排版。转换在独立临时目录使用原件副本和每次独立的配置文件，不改系统 LibreOffice 配置，禁用宏和自动更新文档链接；关闭引擎自动更新检查和 OpenCL，避免转换期间触发组件维护并减少 GPU 依赖。复杂排版、字体缺失和分页仍需用户核对。图片/文档混选被能力表阻止；文档引擎缺失时隐藏目标、禁用计划并提供前往引擎管理入口，安装成功后立即刷新。

## 运行文件与获取

- NuGet 依赖由集中版本和锁文件还原；FFmpeg 在构建时由 `scripts/restore-processing-engines.ps1` 按 `manifest.json` 下载并校验。
- 开发下载缓存：`artifacts/dependencies/ffmpeg/<version>/x64/bundle/`。
- 随软件交付：安装目录 `Engines/ffmpeg/bin/`（EXE 和配套 DLL），NuGet 原生库由运行时资产复制；无需系统 PATH 或用户另装环境。
- 用户点击下载的包：`%LOCALAPPDATA%/FileAssistant/Engines/<id>/<version>/<installation>/`，同级 `<id>.json` 指向已校验的有效安装；后续调用优先使用该安装。修复不覆盖正在使用的版本。
- 可删除的预览缓存：`%LOCALAPPDATA%/FileAssistant/Previews/`，按源路径、大小、修改时间、预览尺寸区分，保留最近 200 项。
- 用户输出位置由转换参数指定，与引擎和缓存目录无关。输出区、任务和各输入列表目前只保存本次运行记录，清空输出记录保留磁盘文件。

运行时只有用户点击设置中的下载/修复才访问固定清单 URL。预览、解码、处理和输出均使用本地文件，不调用在线转换服务；FFmpeg 输入协议仅允许 `file,pipe`。首次构建下载依赖需要联网，不属于运行时转换请求。

清单选用 2026-09-30 的月末构建，上游[保留规则](https://github.com/BtbN/FFmpeg-Builds#release-retention-policy)为月末包保留两年、普通日构建只保留 14 天；不使用浮动 latest 绕过校验。正式发布前应将已审计组件及对应源码材料转存到产品维护的固定分发源，并定期升级。上游目前声明 Windows 10 22H2 为最低支持版本，本项目实际验证环境为 Windows 11 x64。

## 后续专业引擎

专业引擎不混入基础包、不在选择文件时静默下载；设置页复用下载进度、取消、校验与激活流程。当前可下载 FFmpeg 修复包和 LibreOffice 文档包；专业相机 RAW、更新历史和卸载界面仍未接入。

文档包固定官方归档 `26.2.6.3`（373,252,096 字节，SHA-256 `F9877032FD908BEB9C0DDF06DF4AF5C2E85F419C42E14876C4CCE5AAE5FB2660`），已核对 The Document Foundation Authenticode 签名。安装器校验后调用 Windows Installer `/a` 仅解包到私有目录，不注册系统 Office、不改文件关联；保留整包声明和私有 C++ 运行库。取消发生在解包阶段时，等 Windows Installer 结束再清理，不激活取消的安装。下载约 373 MB，解包后的运行文件约 1.2 GB。开发机单独运行 `scripts/test.ps1 -Office` 准备缓存并测试，不随普通构建下载。

升级时同步 NuGet 版本、锁文件、清单、原生依赖及许可声明；重新验证干净缓存下的预览、文件互转、PDF 页序、透明背景、中文路径、原件校验、冲突与取消。正式离线验收应在断网且空缓存下进行，不能把有缓存结果或开发机联网测试写成断网通过。

第三方声明在 `third-party/licenses/`，随应用复制到 `Licenses/`；FFmpeg 保留原包 LICENSE 和文档。来源、许可与发布边界见 `third-party/README.md`。当前仅验证 Windows x64；最终 Release、MSIX 安装和完整分发审计需另验。
