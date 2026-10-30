using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public sealed class SearchResultItem(SearchFile file, SearchColumnWidths? columns = null)
{
    public SearchFile File { get; } = file;
    public SearchColumnWidths Columns { get; } = columns ?? new();
    public string Name => File.Name;
    public string Directory => File.Directory;
    public string FullPath => File.FullPath;
    public string SizeLabel => File.IsFolder ? "—" : File.Size is { } size ? WorkspaceFile.FormatSize(size) : "未索引";
    public string ModifiedLabel => File.Modified?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "未索引";
    public string TypeLabel => File.IsFolder ? "文件夹" : File.Extension.Length == 0 ? "文件" : File.Extension.ToUpperInvariant() + " 文件";
    public string Glyph => File.IsFolder ? "\uE8B7" : Category(File) switch { 1 => "\uEB9F", 3 => "\uF012", 4 => "\uE714", 5 => "\uE8D6", _ => "\uE8A5" };
    public static int Category(SearchFile file)
    {
        if (file.IsFolder) return 0;
        foreach (var kind in new[] { SearchKind.Images, SearchKind.Documents, SearchKind.Archives, SearchKind.Video, SearchKind.Audio })
            if (SearchRules.Extensions(kind).Split(';').Contains(file.Extension)) return (int)kind;
        return 0;
    }
    public override string ToString() => $"{Name}，{TypeLabel}，{SizeLabel}，{Directory}";
}
