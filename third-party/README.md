# 第三方组件与初始化来源

记录当前已经引入的组件。SQLite、7-Zip 尚未接入；图片、视频首帧和 PDF 引擎已接入，见下文。应用自身的许可证仍未决定。

## 直接依赖

版本统一记录在 [Directory.Packages.props](../Directory.Packages.props)，传递依赖及内容校验值记录在各源码项目的 `packages.lock.json`。

| 组件 | 版本 | 用途 | 许可来源 |
| --- | --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM 属性通知和命令生成 | 包内 MIT [许可证](./licenses/CommunityToolkit.Mvvm-8.4.2.txt) |
| Microsoft.WindowsAppSDK | 2.6.4 | WinUI 和 Windows App SDK | 包内 [SDK 许可](./licenses/Microsoft.WindowsAppSDK-2.6.4.txt)及[第三方声明](./licenses/Microsoft.WindowsAppSDK-2.6.4-NOTICE.txt) |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2957 | Windows 资源编译及打包工具 | NuGet 元数据指向的 [Windows SDK 许可](https://aka.ms/WinSDKLicenseURL) |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.7.1 | 命令行启动、调试与打包集成 | NuGet 元数据声明 MIT；[项目源码](https://github.com/microsoft/WinAppCli) |

Windows App SDK 的开源源码许可与 NuGet 分发包所附条款分别记录，不把所有 Windows SDK 二进制文件一概标成 MIT。

## 搜索引擎与测试依赖（2026-10-30）

本应用按[官方 IPC 协议](https://www.voidtools.com/support/everything/sdk/ipc/)调用随包分发的 Everything 1.4.1.1032 x64 便携引擎，协议字段参照官方 SDK 的 `ipc/everything_ipc.h`。未复制 SDK 源码或打包 SDK DLL，不使用 Everything Server。用户已确认允许本软件自动管理后台引擎，无需另外安装 Everything。

桌面程序的[官方许可文本](https://www.voidtools.com/License.txt)允许商业使用及分发，并要求保留版权和许可声明；这不代表其完整核心源码已经公开。原始[许可与 PCRE 声明](./licenses/Everything-1.4.1.1032.txt)随 EXE 和语言包复制到发布输出的 `SearchEngine`；提权安装辅助服务时也复制此声明。不能把本地 IPC 与 Server 企业授权混为一谈。

`scripts/restore-search-engine.ps1` 是构建依赖获取入口：只从官方固定 URL 获取 x64 便携包，校验归档、EXE SHA-256 和 Authenticode 发布者。下载缓存位于忽略的 `artifacts/dependencies`，通过 App 项目 Content 项进入构建、发布与打包输出，运行时无需下载。

- 归档 SHA-256：`698DF475EC44E638F66F1B6A32D28FEA613CEC78D3B6310E6ABE53431EEB940C`
- EXE SHA-256：`F191F756996A14A11E5445FA7103D302EFD510CF2FBF920E6C0C8ED51D512E36`
- 原始许可 SHA-256：`C13D19ADCBFD5D07E9512DE9DF99956A3423399ED1FADC5FD33186697AD8DF2F`

版本升级时需同时核对构建脚本、`EverythingDistribution` 的固定哈希及 `EverythingCache` 路径，重新完成集成测试。当前引擎分发仅验证 x64；MSIX 服务安装和卸载集成仍待正式发布验证。

`scripts/test-everything.ps1` 的开发测试缓存与发布依赖分开，核对[官方 SHA-256](https://www.voidtools.com/Everything-1.4.1.1032.sha256)与 Authenticode 签名。独立实例索引生成的属性与类型样本，生命周期测试另建临时文件夹，不安装测试服务；可执行扩展名样本仅用于元数据过滤，从不执行。测试产物留在忽略的 `artifacts/everything-test`，不进入发布包。

测试工程新增 Microsoft.NET.Test.Sdk 17.14.1（NuGet 元数据为 MIT）、xunit 2.9.3 与 xunit.runner.visualstudio 3.1.4（NuGet 元数据均为 Apache-2.0），仅供测试使用，版本与传递依赖记录在集中配置及各测试项目的锁文件中。许可已核对本机还原包的 `.nuspec`，不计入应用运行时依赖。

## 本地预览与转换引擎（2026-10-30）

版本和调用位置见 [engines/README.md](../engines/README.md)，NuGet 内容由锁文件固定；FFmpeg 由 [manifest.json](../engines/manifest.json) 固定下载地址、版本及 SHA-256。当前仅验证 Windows x64。

| 组件 | 版本 | 当前用途 | 许可与声明 |
| --- | --- | --- | --- |
| Magick.NET-Q16-x64 | 15.0.0 | 图片解码、预览和 PNG/JPEG/WebP/BMP 编码 | 包元数据为 Apache-2.0；保留包内 [Copyright](licenses/Magick.NET-15.0.0-Copyright.txt) 和包含 ImageMagick / 原生依赖声明的 [NOTICE](licenses/Magick.NET-15.0.0-NOTICE.txt) |
| PDFiumCore | 157.0.8100 | .NET PDFium 调用封装 | [对应源码提交的 Apache-2.0 许可](licenses/PDFiumCore-157.0.8100.txt)，来源提交 `9818b101e713409e033b5489cb41c949e953dced` |
| bblanchon.PDFium.Win32 / PDFium | 157.0.8100 | PDF 首页渲染与逐页导出 | 保留[二进制分发声明](licenses/PDFium-157.0.8100-NOTICES.txt)和[原生第三方许可目录](licenses/PDFium-157.0.8100/)，其中 `pdfium.txt` 是 PDFium 自身的 BSD 风格许可 |
| PdfPig | 0.1.16 | 图片合并成 PDF | [对应源码提交的 Apache-2.0 许可](licenses/PdfPig-0.1.16.txt)，来源提交 `a7bb35662bbbf405efddad50aedc9bcdcf515afc` |
| FFmpeg（BtbN LGPL shared 构建） | 8.1.3-9-g29e619e767 | 视频首帧 | 原包 LGPL-3.0 LICENSE 与文档随 `Engines/ffmpeg/` 交付；校验 `-buildconf` 无 `--enable-gpl` / `--enable-nonfree`，不据此将全部依赖统称为 Apache 或 MIT |

PDFium 声明取自固定 [chromium/8100 发行包](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium/8100)；该包 `bin/pdfium.dll` 与 NuGet 的 win-x64 DLL SHA-256 相同：`AE913F407E8D3F5CDBF08A9869564D647B13E132E01EB376C0A5A33B7A227A45`。声明中的 `licenses/` 相对路径对应本仓库同版本许可目录；应用输出将声明统一复制至 `Licenses/`，文件名中的版本点替换为下划线，以免被 PRI 当作资源限定符。

FFmpeg [固定二进制包](https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-30-13-08/ffmpeg-n8.1.3-9-g29e619e767-win64-lgpl-shared-8.1.zip)为 80,733,362 字节；[源码提交](https://github.com/FFmpeg/FFmpeg/commit/29e619e767)及[构建配方](https://github.com/BtbN/FFmpeg-Builds/tree/autobuild-2026-09-30-13-08)记录在清单。当前为本地开发接入，尚未对外发布；正式分发前须按实际 LGPL 构建及所有外部库补齐对应源码获取/分发材料、构建配置和完整许可清单，不能将构建通过等同于分发审计通过。

## 工程生成来源

- 应用使用微软 `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` **0.0.7-alpha** 中的 `winui-mvvm` 模板生成。[官方模板说明](https://github.com/microsoft/WindowsAppSDK/blob/main/dev/Templates/Dotnet/README.md)
- 模板包元数据声明 MIT，仓库提交为 `164ad9681a9133f056ff38a43609e6227121e91a`；保留该提交的[许可证](./licenses/WinUI-Templates-0.0.7-alpha.txt)。
- Core、Infrastructure、解决方案和 SDK 配置使用 .NET SDK 的 `classlib`、`sln`、`globaljson` 模板生成。
- 模板包属于预发布版本；它用于生成起始源码，已经生成的源码随仓库维护。普通构建不需要再次安装模板包。

## 维护方式

更新依赖时同步版本和锁文件，核对新版本随附的许可与声明。正式分发前，根据实际安装包包含的运行时和转换引擎补齐完整清单；当前文件记录初始化阶段的直接依赖，不代表最终发布清单。
