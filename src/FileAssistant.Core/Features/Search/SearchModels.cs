namespace FileAssistant.Core.Features.Search;

public enum SearchScope { Everything, Folder, Batch }
public enum SearchKind { All, Images, Documents, Archives, Video, Audio, Folders, Files, Executables }
public enum SearchSort { Name, Path, Size, Modified }

public sealed record SearchFile(string FullPath, bool IsFolder, long? Size, DateTimeOffset? Modified)
{
    public string Name => Path.GetFileName(FullPath.TrimEnd('\\', '/'));
    public string Directory => Path.GetDirectoryName(FullPath.TrimEnd('\\', '/')) ?? FullPath;
    public string Extension => IsFolder ? string.Empty : Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
}

public sealed record SearchRequest
{
    public string Text { get; init; } = string.Empty;
    public SearchScope Scope { get; init; }
    public string Folder { get; init; } = string.Empty;
    public bool IncludeSubfolders { get; init; } = true;
    public bool MatchPath { get; init; }
    public SearchKind Kind { get; init; }
    public long? MinimumSize { get; init; }
    public long? MaximumSize { get; init; }
    public DateTimeOffset? ModifiedFrom { get; init; }
    public DateTimeOffset? ModifiedBefore { get; init; }
    public SearchSort Sort { get; init; }
    public bool Descending { get; init; }
    public int Offset { get; init; }
    public int PageSize { get; init; } = 200;
}

public sealed record SearchPage(IReadOnlyList<SearchFile> Items, long Total, string Source, int Skipped = 0)
{
    public bool HasMore(int loaded) => loaded < Total;
}

public sealed record SearchProgress(int Examined, string Location, string? Message = null);

public sealed class SearchException(string message) : Exception(message);
