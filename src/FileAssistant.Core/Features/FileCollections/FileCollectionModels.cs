using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Features.FileCollections;

public sealed record CollectionFolder(Guid Id, Guid ParentId, string Name);
public sealed record CollectionEntry(Guid Id, Guid FolderId, SearchFile File, string? Error = null);
public sealed record FileCollectionSnapshot(int Version, IReadOnlyList<CollectionFolder> Folders,
    IReadOnlyList<CollectionEntry> Entries, Guid CurrentFolderId = default);
public sealed record FileCollectionBundle(IReadOnlyList<CollectionFolder> Folders, IReadOnlyList<CollectionEntry> Entries)
{
    public bool HasItems => Folders.Count > 0 || Entries.Count > 0;
    public IReadOnlyList<SearchFile> Files => Entries.Select(entry => entry.File).DistinctBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
    public static FileCollectionBundle FromFiles(IReadOnlyList<SearchFile> files) => new([], files.DistinctBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase).Select(file => new CollectionEntry(Guid.NewGuid(), Guid.Empty, file)).ToArray());
}
public sealed record CollectionLoadResult(FileCollectionSnapshot? Snapshot, string? Message = null, bool CanSave = true);
public interface IFileCollectionStore
{
    Task<CollectionLoadResult> LoadAsync(CancellationToken token);
    Task SaveAsync(FileCollectionSnapshot snapshot, CancellationToken token);
}
