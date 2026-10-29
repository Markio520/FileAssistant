# 第三方组件与初始化来源

记录当前已经引入的组件。产品方案中的 SQLite、7-Zip、图片和 PDF 引擎尚未接入。应用自身的许可证仍未决定。

## 直接依赖

版本统一记录在 [Directory.Packages.props](../Directory.Packages.props)，传递依赖及内容校验值记录在各源码项目的 `packages.lock.json`。

| 组件 | 版本 | 用途 | 许可来源 |
| --- | --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM 属性通知和命令生成 | 包内 MIT [许可证](./licenses/CommunityToolkit.Mvvm-8.4.2.txt) |
| Microsoft.WindowsAppSDK | 2.6.4 | WinUI 和 Windows App SDK | 包内 [SDK 许可](./licenses/Microsoft.WindowsAppSDK-2.6.4.txt)及[第三方声明](./licenses/Microsoft.WindowsAppSDK-2.6.4-NOTICE.txt) |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2957 | Windows 资源编译及打包工具 | NuGet 元数据指向的 [Windows SDK 许可](https://aka.ms/WinSDKLicenseURL) |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.7.1 | 命令行启动、调试与打包集成 | NuGet 元数据声明 MIT；[项目源码](https://github.com/microsoft/WinAppCli) |

Windows App SDK 的开源源码许可与 NuGet 分发包所附条款分别记录，不把所有 Windows SDK 二进制文件一概标成 MIT。

## 工程生成来源

- 应用使用微软 `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` **0.0.7-alpha** 中的 `winui-mvvm` 模板生成。[官方模板说明](https://github.com/microsoft/WindowsAppSDK/blob/main/dev/Templates/Dotnet/README.md)
- 模板包元数据声明 MIT，仓库提交为 `164ad9681a9133f056ff38a43609e6227121e91a`；保留该提交的[许可证](./licenses/WinUI-Templates-0.0.7-alpha.txt)。
- Core、Infrastructure、解决方案和 SDK 配置使用 .NET SDK 的 `classlib`、`sln`、`globaljson` 模板生成。
- 模板包属于预发布版本；它用于生成起始源码，已经生成的源码随仓库维护。普通构建不需要再次安装模板包。

## 维护方式

更新依赖时同步版本和锁文件，核对新版本随附的许可与声明。正式分发前，根据实际安装包包含的运行时和转换引擎补齐完整清单；当前文件记录初始化阶段的直接依赖，不代表最终发布清单。
