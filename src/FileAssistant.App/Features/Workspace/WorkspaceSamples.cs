using System.Collections.Generic;

namespace FileAssistant.App.Features.Workspace;

/// <summary>Synthetic fixtures for reviewing the interface; these paths are never opened.</summary>
internal static class WorkspaceSamples
{
    public static IEnumerable<WorkspaceFile> Create() =>
    [
        new("秋日山野_封面.jpg", "JPG 图片", 3_420_000, @"D:\示例项目\秋日素材\图片", "2026/10/28  14:32", "\uEB9F", 1),
        new("产品主图_最终确认版_用于官网与社交媒体发布_20261029.png", "PNG 图片", 8_260_000, @"D:\示例项目\秋日素材\图片", "2026/10/29  09:18", "\uEB9F", 1),
        new("项目说明.pdf", "PDF 文档", 1_280_000, @"D:\示例项目\交付文档", "2026/10/28  16:45", "\uEA90", 2),
        new("交付清单.xlsx", "Excel 表格", 48_200, @"D:\示例项目\交付文档", "2026/10/29  10:06", "\uE9F9", 2),
        new("拍摄计划.docx", "Word 文档", 126_400, @"D:\示例项目\交付文档", "2026/10/27  11:20", "\uE8A5", 2),
        new("使用说明.txt", "文本文件", 2_400, @"D:\示例项目\交付文档", "2026/10/28  18:12", "\uE8A5", 2),
        new("秋日素材_归档.zip", "ZIP 压缩包", 86_400_000, @"D:\示例项目\归档", "2026/10/28  18:30", "\uF012", 3),
        new("镜头 03_林间光影.mp4", "MP4 视频", 128_800_000, @"D:\示例项目\秋日素材\视频", "2026/10/27  15:52", "\uE714", 4)
    ];
}
