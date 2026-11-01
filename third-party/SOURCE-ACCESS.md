# 对应源码与许可获取说明

本项目自有代码的许可证尚未确定。调用第三方程序或库不改变它们各自的许可证；不将本文件解释为项目已采用 MPL、LGPL 或 Apache 许可。原始声明在 `third-party/licenses/`，构建将它们复制到运行目录的 `Licenses/`；引擎自己的许可继续随引擎目录保留。

| 实际组件 | 对应源码 / 获取路径 |
| --- | --- |
| LibreOffice 26.2.6.3 | [固定源码提交 8221e31b3ac356a1623c672912a3d2b492f7e3d1](https://github.com/LibreOffice/core/tree/8221e31b3ac356a1623c672912a3d2b492f7e3d1)；[官方版本归档](https://downloadarchive.documentfoundation.org/libreoffice/old/26.2.6.3/) 中的 `src/` 与该版第三方源码归档；组件内 `license.txt` 和 `NOTICE` 保留 |
| FFmpeg 8.1.3-9-g29e619e767 | [固定 FFmpeg 提交 29e619e767](https://github.com/FFmpeg/FFmpeg/commit/29e619e767)；[BtbN 2026-09-30 构建脚本与依赖配方](https://github.com/BtbN/FFmpeg-Builds/tree/autobuild-2026-09-30-13-08)，依照配方获取外部库对应版本与补丁；本机包的完整选项见 [构建配置](ffmpeg-build-configuration.txt) |
| Magick.NET 15.0.0 | [NuGet 元数据记录的固定提交 79bb8700d7fe3a564f62926ab299fc2520cb2269](https://github.com/dlemstra/Magick.NET/tree/79bb8700d7fe3a564f62926ab299fc2520cb2269)；原生 ImageMagick 及依赖的版本和许可见实际 NuGet 包的 `NOTICE`，仓库已保存；不用 Magick.NET 的许可证替代其原生依赖声明 |
| PDFiumCore 157.0.8100 | [固定源码提交](https://github.com/Dtronix/PDFiumCore/tree/9818b101e713409e033b5489cb41c949e953dced) |
| PDFium chromium/8100 | [固定二进制发行与构建项目](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium/8100)；该发布记录的 PDFium/Chromium 源码及各依赖许可；完整原生声明见 `licenses/PDFium-157.0.8100/` |
| PdfPig 0.1.16 | [固定源码提交 a7bb35662bbbf405efddad50aedc9bcdcf515afc](https://github.com/UglyToad/PdfPig/tree/a7bb35662bbbf405efddad50aedc9bcdcf515afc) |

当前接入使用未修改的第三方二进制；LibreOffice 和 FFmpeg 通过独立进程调用。LibreOffice 只在用户点击安装/修复时获取，转换不下载引擎或上传文件。

开发构建的源码索引不等于完整对外分发审计。对外发布含 FFmpeg 或相关原生库的安装包前，需要归档与实际二进制对应的完整源码、外部依赖、补丁和构建材料，并核对各许可要求的交付方式；不能仅用一个上游主页链接替代对应源码。本轮没有发布安装包、授予项目许可证、提交或推送 GitHub。
