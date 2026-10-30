using FileAssistant.App.Controls;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public static class SearchFilterOptions
{
    public static IReadOnlyList<SelectionOption> Scopes { get; } =
    [
        new((int)SearchScope.Everything, "全部已索引位置"),
        new((int)SearchScope.Folder, "指定目录"),
        new((int)SearchScope.Batch, "文件暂存区")
    ];
    public static IReadOnlyList<SelectionOption> Kinds { get; } =
    [
        new((int)SearchKind.All, "所有", "所有类型"),
        new((int)SearchKind.Audio, "音频"), new((int)SearchKind.Archives, "压缩文件"),
        new((int)SearchKind.Documents, "文档"), new((int)SearchKind.Executables, "可执行文件"),
        new((int)SearchKind.Folders, "文件夹"), new((int)SearchKind.Images, "图片"),
        new((int)SearchKind.Video, "视频"), new((int)SearchKind.Files, "仅文件")
    ];
    public static IReadOnlyList<SelectionOption> Sizes { get; } =
    [
        new(0, "不限大小"), new(1, "小于 1 MB"), new(2, "1–10 MB"),
        new(3, "至少 10 MB"), new(4, "至少 100 MB"), new(5, "至少 1 GB"), new(6, "自定义大小")
    ];
    public static IReadOnlyList<SelectionOption> Dates { get; } =
    [
        new(0, "不限修改日期"), new(1, "今天修改"), new(2, "最近 7 天"), new(3, "最近 30 天"), new(4, "自定义日期")
    ];
    public static IReadOnlyList<SelectionOption> SizeUnits { get; } =
    [new(0, "B"), new(1, "KB"), new(2, "MB"), new(3, "GB")];
    public static IReadOnlyList<SelectionOption> Sorts { get; } =
    [
        new(0, "名称 · 升序"), new(1, "名称 · 降序"), new(2, "路径 · 升序"), new(3, "路径 · 降序"),
        new(4, "大小 · 从小到大"), new(5, "大小 · 从大到小"), new(6, "修改 · 从旧到新"), new(7, "修改 · 从新到旧")
    ];
}
