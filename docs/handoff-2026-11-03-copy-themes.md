# 复制与整理、拖放修复及默认配色交接

更新：2026-11-03，Asia/Shanghai。继续本阶段先读项目根目录 `AGENTS.md` 和本文；此前工作区记录见 [11-02 交接](handoff-2026-11-02-workspace.md)。

## 交付范围

用户本次明确授权提交并推送 GitHub，仓库 `Markio520/FileAssistant`，分支 `main`，本轮基于 `1b5ee33`。保存源码、测试、设计说明与本交接；不发布安装包。本次授权不形成永久自动推送或自动重启规则。

- 复制与整理：独立输入、自动扫描计划、三种目录模式、编号/跳过/逐项确认覆盖、执行取消、未完成项重试、任务与输出闭环，覆盖备份与恢复记录跨重启读取。详细行为和限制见 [复制与整理](copy-organize.md)。
- 公共输入：筛选保留隐藏勾选并明确范围；显式清除勾选。左侧文件夹栏分隔线支持拖动调宽，收起恢复保留本次宽度。
- 软件文件夹：为“拖入后单击分离”和“难以拖入”改为整行直接转发拖放事件，保留循环检查、模型移动、撤销与自动保存。首次修复用户复验失败，最终修复仍待原生验收，不能标为已彻底解决。
- 默认浅色为用户选定的 **15 沙白画廊**，深色为 **14 炭黑微光**。继续沿用跟随系统/浅色/深色切换。配色集中于 `Resources/ThemePalette.xaml`，语义与 Fluent 画刷在 `WorkspaceTheme.xaml`；高对比度使用系统颜色。深色主按钮红色略加深为 `#C94B46`，强调文字使用更亮的 `#EF6B63`，以保证可读性。

已选概念图随仓库保存：[浅色 15](design-reference/2026-11-03-defaults/light-15.png)、[深色 14](design-reference/2026-11-03-defaults/dark-14.png)。图片是设计参考，含示例文件，不能视为正式程序截图；本轮仅落地配色，不照搬示例内容或参考产品标识。

## 关键实现与维护入口

| 文件/目录 | 职责 |
| --- | --- |
| Core `Features/Organize/CopyModels.cs`、`CopyPlanner.cs` | 复制计划、路径映射、范围与冲突规则 |
| Infrastructure `FileSystem/CopyScanner.cs`、`CopyService.cs`、`CopyFileSystem.cs`、`WindowsCopyFile.cs` | 扫描、Windows CopyFile2、取消、临时文件、内容校验与提交 |
| Infrastructure `FileSystem/CopyRecoveryStore.cs` | 覆盖前恢复清单、恢复与备份清理 |
| App `Features/Organize/CopyViewModel.cs`、`CopyView.*`、`CopyOptionsView.*` | 参数、自动计划、冲突确认、执行结果及恢复交互 |
| App `Features/Tasks/ProcessingJob.cs`、`ProcessingJobsViewModel.cs` | 通用任务名称、字节进度、结果明细，复制与转换共用队列 |
| App `Features/Workspace/WorkspaceViewModel.cs` | 选择范围与隐藏勾选保持 |
| App `Controls/DropTargetSurface.cs`、`FileInputView.Folders.cs` | 整行拖放直接转发、落点与模型同步 |
| App `FileInputView.Layout.cs`、`FileInputView.Display.cs` | 公共侧栏尺寸与恢复 |
| App `Resources/ThemePalette.xaml`、`WorkspaceTheme.xaml` | 默认配色、按主题的系统强调色阶及语义画刷 |

复制先写同目标位置的任务临时文件，校验主数据流长度和 SHA-256 后再提交；覆盖先保存恢复清单及旧目标备份。取消保留已提交文件，清理未提交临时文件。目标变化重新检查冲突。成功和主动跳过项不重试，源文件不删除。重解析点不跟随；任务和输出历史仍仅本次运行有效，不支持跨重启续传。移动、按日期分类和按数量分组留待下一阶段。

## 验证结果

本次提交前重新运行 `scripts/test.ps1`：**294 项通过，0 失败、0 跳过**，Core 83、Infrastructure 99、App 112。覆盖复制计划/执行/恢复/重试、分组模型及保存重载；模型测试不能替代原生拖放验收。

最新配色源码的 Debug/x64 打包及非打包自包含构建均 **0 警告、0 错误**。静态检查确认浅色、深色、高对比度资源键配对，浅深色各 7 组主要文字/按钮对比通过。构建与静态检查不等同于原生显示验收。

本机日志（`artifacts` 被 Git 忽略，不随仓库分发）：

- `artifacts/handoff-20261103-tests.log`
- `artifacts/theme-defaults-20261103-build.log`
- `artifacts/theme-defaults-20261103-unpackaged.log`
- `artifacts/folder-drop-20261103/observed.jsonl`：人工拖动诊断；临时诊断源码已移除。

Office、Everything 独立集成测试及物理断网未在本轮重跑；大量小文件/单个大文件压力、真实磁盘满、设备断开、Release、安装签名与完整分发仍待专项验收。

## 本机运行入口与换机续接

**本机最新完整源码对应的非打包入口**：

```text
src/FileAssistant.App/bin/theme-defaults/x64/Debug/net10.0-windows10.0.26100.0/win-x64/FileAssistant.App.exe
```

它包含复制、最终拖放实现及默认配色。当前已打开窗口仍可能是 `bin/drag-fix` 版本，不具备最新配色；旧 `bin/unpackaged` 曾保留中间诊断版，`scripts/run-next-stage.ps1` 仍指向该旧目录，不能据此判断最新实现。先让用户正常关闭程序、补写集合，再打开最新入口；不要强制结束进程，不在运行时覆盖集合 JSON。

仓库不包含构建产物。换机后在项目根目录使用已固定的 .NET/Windows SDK，执行：

```powershell
./scripts/check-environment.ps1
./scripts/test.ps1
./scripts/build.ps1
# 确认已有软件实例已正常关闭后，由用户启动：
./scripts/run.ps1 -Unpackaged
```

此处 `run.ps1 -Unpackaged` 会重建正常 `bin/unpackaged` 目录；本机隔离目录只是避免覆盖运行中的旧实例，不是发布路径。依赖与引擎准备遵循 [开发环境](development-environment.md) 和 `engines/README.md`，首次恢复需要联网。

## 下一步按顺序验收

1. 用最新窗口将空软件分组拖入另一个分组，落点分别覆盖图标、文字、数量、留白与行间缝隙；松开后单击/切换/刷新不能分离；再测移回根与空白、循环拒绝、撤销及正常重启恢复。仅移动视觉节点不算成功，须核对已保存父子关系。
2. 本轮遗留两个空测试分组在复制输入：`拖放验收A`（`cf4e2673-8c74-4ca0-a32b-9b2bab24986b`）、`拖放验收B`（`d9802b1a-b8e7-4587-9ae2-6fddd68f83a1`）。验收后通过软件清理这两个测试分组，保留用户原分组与全部文件引用。不要整体回滚历史 JSON 快照。
3. 原生切换浅色/深色/跟随系统，检查主按钮、导航、文件夹/列表选中、筛选下拉、弹窗、任务/输出、标题栏及高对比度。颜色仍只从公共主题修改。
4. 在临时磁盘目录验收复制与取消、目标内/批内冲突、覆盖备份恢复、未完成项重试及输出操作；核对内容一致、源未变、半成品未被当成成功、重试不重复成功产物。
5. 单独安排大量小文件、大文件、空间不足、目标断开及发布验收。移动功能只有实际移动成功后才更新输入引用；跨盘必须先复制校验再删除源，尚未实施。

软件分组位置为 `%LOCALAPPDATA%/FileAssistant/Collections/{staging,copy,conversion}.json`；复制恢复清单为 `%LOCALAPPDATA%/FileAssistant/CopyRecovery/{id}.json`，备份在目标同目录 `.fileassistant-backup-{id}.bak`。文件列表与恢复数据均不得提交 GitHub。
