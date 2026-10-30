namespace FileAssistant.App.Features.Workspace;

/// <summary>Display-only information, separate from future Core file-operation models.</summary>
public sealed record WorkspaceFile(string Name, string Type, long Size, string Directory, string Modified, string Glyph, int Category,
    FileAssistant.Core.Features.Search.SearchFile? Source = null)
{
    public bool IsSample => Source is null;
    public string SizeLabel => Source?.IsFolder == true ? "—" : Size < 0 ? "未索引" : FormatSize(Size);
    public string FullPath => Source?.FullPath ?? System.IO.Path.Combine(Directory, Name);

    public override string ToString() => $"{Name}，{Type}，{SizeLabel}";

    public static string FormatSize(long size) => size switch
    {
        >= 1024 * 1024 * 1024 => $"{size / (1024d * 1024 * 1024):0.##} GB",
        >= 1024 * 1024 => $"{size / (1024d * 1024):0.##} MB",
        >= 1024 => $"{size / 1024d:0.#} KB",
        _ => $"{size} B"
    };
}
