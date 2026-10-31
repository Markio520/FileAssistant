using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.FileCollections;

namespace FileAssistant.App.Features.FileInput;

/// <summary>A dialog-local selection that survives browsing into another folder.</summary>
public sealed class CollectionSourceSelection
{
    private readonly FileCollection collection;
    private readonly HashSet<Guid> folderIds = [];
    private readonly HashSet<Guid> entryIds = [];
    public CollectionSourceSelection(FileCollectionSnapshot snapshot) => collection = FileCollection.Restore(snapshot);
    public int Count => folderIds.Count + entryIds.Count;
    public FileCollectionBundle Bundle => collection.Export(Guid.Empty, folderIds, entryIds);
    public bool Contains(WorkspaceFile item) => item.FolderId is { } folder ? folderIds.Contains(folder) : item.EntryId is { } entry && entryIds.Contains(entry);
    public void UpdateVisible(IEnumerable<WorkspaceFile> visible, IEnumerable<WorkspaceFile> selected)
    {
        foreach (var item in visible)
        { if (item.FolderId is { } folder) folderIds.Remove(folder); if (item.EntryId is { } entry) entryIds.Remove(entry); }
        foreach (var item in selected)
        { if (item.FolderId is { } folder && collection.HasFolder(folder)) folderIds.Add(folder); if (item.EntryId is { } entry && collection.Entries.Any(value => value.Id == entry)) entryIds.Add(entry); }
    }
    public void BrowseInto(Guid folder) => folderIds.Remove(folder);
    public void Clear() { folderIds.Clear(); entryIds.Clear(); }
}
