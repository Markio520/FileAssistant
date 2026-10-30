using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Features.FileInput;

public enum FileImportStatus { Ready, Added, Duplicate, Unsupported, Unavailable }

public sealed record FileImportItem(string Path, FileImportStatus Status, string Reason, SearchFile? File = null)
{
    public string StatusLabel => Status switch
    {
        FileImportStatus.Ready => "可用", FileImportStatus.Added => "已加入", FileImportStatus.Duplicate => "重复",
        FileImportStatus.Unsupported => "不支持", _ => "失效或不可访问"
    };
}

public sealed record FileImportReport(IReadOnlyList<FileImportItem> Items, string? Notice = null)
{
    public static FileImportReport Empty { get; } = new([]);
    public int Added => Items.Count(item => item.Status == FileImportStatus.Added);
    public int Duplicates => Items.Count(item => item.Status == FileImportStatus.Duplicate);
    public int Unsupported => Items.Count(item => item.Status == FileImportStatus.Unsupported);
    public int Unavailable => Items.Count(item => item.Status == FileImportStatus.Unavailable);
    public bool HasIssues => Duplicates + Unsupported + Unavailable > 0;
    public string Summary => Notice ?? $"已加入 {Added:N0} 项 · 重复 {Duplicates:N0} 项 · 不支持 {Unsupported:N0} 项 · 失效或不可访问 {Unavailable:N0} 项";
}
