using FileAssistant.App.Controls;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.FileInput;

/// <summary>File type choices shared by search, staging and independent feature inputs.</summary>
public static class FileTypeFilterOptions
{
    public static IReadOnlyList<SelectionOption> All { get; } =
    [
        new((int)SearchKind.All, "所有", "所有类型"),
        new((int)SearchKind.Audio, "音频"), new((int)SearchKind.Archives, "压缩文件"),
        new((int)SearchKind.Documents, "文档"), new((int)SearchKind.Executables, "可执行文件"),
        new((int)SearchKind.Folders, "文件夹"), new((int)SearchKind.Images, "图片"),
        new((int)SearchKind.Video, "视频"), new((int)SearchKind.Files, "仅文件")
    ];
}
