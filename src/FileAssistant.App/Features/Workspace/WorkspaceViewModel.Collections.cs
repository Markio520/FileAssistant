using CommunityToolkit.Mvvm.ComponentModel;
using FileAssistant.Core.Features.FileCollections;
using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Workspace;

public sealed record FolderBreadcrumb(Guid Id, string Name);
public sealed record FolderChoice(Guid Id, string Label) { public override string ToString() => Label; }

public partial class WorkspaceViewModel
{
    private FileCollection collection = new();
    private readonly IFileCollectionStore? collectionStore;
    private bool restoringCollection;
    private bool saveEnabled;
    private int dirtyVersion, savedVersion;
    private CancellationTokenSource? saveCancellation;
    private Task pendingSave = Task.CompletedTask;
    public Guid CurrentFolderId { get; private set; }
    public int CollectionRevision { get; private set; }
    public string CurrentFolderName => CurrentFolderId == Guid.Empty ? Title : collection.Folders.First(folder => folder.Id == CurrentFolderId).Name;
    public string CurrentFolderPath => CurrentFolderId == Guid.Empty ? Title : Title + " / " + collection.FolderPath(CurrentFolderId);
    public bool CanGoUp => CurrentFolderId != Guid.Empty && CanReceiveFiles;
    public bool CanRenameFolder => selectedFiles.Count == 1 && selectedFiles[0].IsCollectionFolder && CanReceiveFiles;
    public IReadOnlyList<Guid> SelectedFolderIds => selectedFiles.Where(file => file.FolderId is { } id && collection.HasFolder(id)).Select(file => file.FolderId!.Value).ToArray();
    public IReadOnlyList<Guid> SelectedEntryIds => selectedFiles.Where(file => file.EntryId is not null).Select(file => file.EntryId!.Value).ToArray();
    public IReadOnlyList<SearchFile> ScopeEntries => collection.Resolve(CurrentFolderId).Select(entry => entry.File).ToArray();
    public IReadOnlyList<SearchFile> EffectiveEntries => SelectionCount > 0 ? SelectedEntries : ScopeEntries;
    public IReadOnlyList<FolderBreadcrumb> Breadcrumbs => collection.Ancestors(CurrentFolderId).Select(id =>
        new FolderBreadcrumb(id, id == Guid.Empty ? Title : collection.Folders.First(folder => folder.Id == id).Name)).ToArray();
    public IReadOnlyList<FolderChoice> FolderChoices => new[] { new FolderChoice(Guid.Empty, Title) }
        .Concat(collection.Folders.Select(folder => new FolderChoice(folder.Id, Title + " / " + collection.FolderPath(folder.Id)))).ToArray();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPersistenceMessage), nameof(HasFooterMessage))]
    public partial string PersistenceMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool CanRetrySave { get; set; }
    public bool HasPersistenceMessage => PersistenceMessage.Length > 0;
    public bool HasFooterMessage => HasInputFeedback || HasPersistenceMessage || HasUndo;
    public bool HasUnsavedCollection => saveEnabled && dirtyVersion != savedVersion;
    public FileCollectionSnapshot CollectionSnapshot => collection.Snapshot(CurrentFolderId);
    public bool HasFolder(Guid id) => collection.HasFolder(id);
    public string FolderName(Guid id) => collection.Folders.First(folder => folder.Id == id).Name;
    public bool CanMoveTo(IReadOnlyList<Guid> folders, Guid target) => collection.CanMove(folders, target);

    private WorkspaceFile EntryRow(CollectionEntry entry)
    {
        var row = WorkspaceFile.FromSource(entry.File);
        return row with { EntryId = entry.Id, ReferenceError = entry.Error, Type = entry.Error is null ? row.Type : row.Type + " · 失效" };
    }
    private WorkspaceFile FolderRow(CollectionFolder folder)
    {
        var children = collection.Resolve(folder.Id);
        return new(folder.Name, $"软件文件夹 · {children.Count:N0} 项", children.Sum(entry => entry.File.Size ?? 0),
            Title + (folder.ParentId == Guid.Empty ? "" : " / " + collection.FolderPath(folder.ParentId)), "—", "\uE8B7", 8,
            FolderId: folder.Id);
    }
    private IEnumerable<WorkspaceFile> CurrentRows()
    {
        if (HasSamples) return files;
        var folders = collection.Folders.Where(folder => folder.ParentId == CurrentFolderId).Select(FolderRow);
        var entries = SearchText.Trim().Length == 0 ? collection.Entries.Where(entry => entry.FolderId == CurrentFolderId)
            : collection.Entries.Where(entry => collection.Ancestors(entry.FolderId).Contains(CurrentFolderId));
        return folders.Concat(entries.Select(EntryRow));
    }
    private bool HasCurrentItems => CurrentRows().Any();
    private void CollectionChanged(bool persist = true)
    {
        if (!collection.HasFolder(CurrentFolderId)) CurrentFolderId = Guid.Empty;
        if (!HasSamples || collection.Entries.Count > 0 || collection.Folders.Count > 0)
        { files.Clear(); files.AddRange(collection.Entries.Select(EntryRow)); }
        CollectionRevision++;
        ApplyFilters();
        foreach (var property in new[] { nameof(SearchEntries), nameof(ScopeEntries), nameof(EffectiveEntries), nameof(CollectionRevision),
            nameof(CurrentFolderId), nameof(CurrentFolderName), nameof(CurrentFolderPath), nameof(CanGoUp), nameof(Breadcrumbs), nameof(FolderChoices), nameof(EmptyTitle) })
            OnPropertyChanged(property);
        if (persist) QueueSave();
    }
    public Guid CreateFolder(string name, Guid? parent = null)
    {
        var before = CollectionSnapshot;
        var id = collection.CreateFolder(parent ?? CurrentFolderId, name);
        RememberChange(before, "新建文件夹");
        files.RemoveAll(file => file.IsSample); CollectionChanged(); return id;
    }
    public void RenameFolder(Guid id, string name) { var before = CollectionSnapshot; collection.RenameFolder(id, name); RememberChange(before, "重命名文件夹"); CollectionChanged(); }
    public void DissolveFolder(Guid id) { var before = CollectionSnapshot; collection.Dissolve(id); RememberChange(before, "解散文件夹"); CollectionChanged(); }
    public void EnterFolder(Guid id)
    {
        if (IsLoading || !collection.HasFolder(id) || CurrentFolderId == id) return;
        CurrentFolderId = id; SearchText = string.Empty; FilterIndex = 0; HasError = false;
        CollectionChanged();
    }
    public void GoUp() { if (CanGoUp) EnterFolder(collection.Parent(CurrentFolderId)); }
    public void MoveSelectionTo(Guid target)
    {
        var before = CollectionSnapshot;
        collection.Move(SelectedFolderIds, SelectedEntryIds, target); RememberChange(before, "移动"); CollectionChanged();
    }
    public void MoveItemsTo(IReadOnlyList<WorkspaceFile> items, Guid target)
    {
        var before = CollectionSnapshot;
        collection.Move(items.Where(item => item.FolderId is not null).Select(item => item.FolderId!.Value),
            items.Where(item => item.EntryId is not null).Select(item => item.EntryId!.Value), target);
        RememberChange(before, "移动"); CollectionChanged();
        InputFeedback = "已移入“" + (target == Guid.Empty ? Title : collection.FolderPath(target)) + "”。";
    }
    public FileCollectionBundle ExportSelection() => collection.Export(CurrentFolderId, SelectedFolderIds, SelectedEntryIds);
    public FileCollectionBundle ExportCurrentFolder() => collection.Export(CurrentFolderId);
    public FileCollectionBundle ExportItems(IReadOnlyList<WorkspaceFile> items) => collection.Export(CurrentFolderId,
        items.Where(item => item.FolderId is not null).Select(item => item.FolderId!.Value), items.Where(item => item.EntryId is not null).Select(item => item.EntryId!.Value));
    public void InitializeCollection(FileCollectionSnapshot snapshot)
    {
        collection = FileCollection.Restore(snapshot);
        undoSnapshot = null; undoAction = string.Empty; NotifyUndoState();
        CurrentFolderId = collection.HasFolder(snapshot.CurrentFolderId) ? snapshot.CurrentFolderId : Guid.Empty;
        files.Clear(); CollectionChanged(persist: false);
    }
    public async Task<FileImportReport> ImportBundleAsync(FileCollectionBundle bundle, Guid destination, bool preserveFolders)
    {
        if (IsLoading) return new([], InputFeedback = "正在添加文件，请稍后重试。");
        if (inputReader is null) throw new InvalidOperationException("File input reader is not configured.");
        FileCollection.Restore(new(1, bundle.Folders, bundle.Entries));
        using var cancellation = new CancellationTokenSource(); importCancellation = cancellation;
        var version = ++importGeneration;
        IsLoading = true; LoadingMessage = "正在检查转交的文件…";
        try
        {
            var report = await inputReader.ReadAsync(bundle.Files.Select(file => file.FullPath).ToArray(), cancellation.Token);
            if (version != importGeneration || cancellation.IsCancellationRequested)
                return new([], "转交已取消，目标列表保持不变。");
            if (!collection.HasFolder(destination)) return new([], InputFeedback = "目标软件文件夹已不存在，转交已取消。");
            var valid = report.Items.Where(item => item.Status == FileImportStatus.Ready && item.File is not null)
                .ToDictionary(item => item.Path, item => item.File!, StringComparer.OrdinalIgnoreCase);
            var prepared = bundle with { Entries = bundle.Entries.Where(entry => valid.ContainsKey(entry.File.FullPath))
                .Select(entry => entry with { File = valid[entry.File.FullPath], Error = null }).ToArray() };
            var before = CollectionSnapshot;
            var previous = collection.Entries.Select(entry => entry.Id).ToHashSet();
            var added = collection.Import(destination, prepared, preserveFolders);
            RememberChange(before, "加入文件");
            var addedPaths = collection.Entries.Where(entry => !previous.Contains(entry.Id)).Select(entry => entry.File.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var items = report.Items.Select(item => item.Status == FileImportStatus.Ready ? item with
                { Status = addedPaths.Contains(item.Path) ? FileImportStatus.Added : FileImportStatus.Duplicate, Reason = addedPaths.Contains(item.Path) ? "已加入目标软件文件夹。" : "目标软件文件夹已有此路径。" } : item).ToArray();
            var summary = $"已加入 {added:N0} 项";
            if (preserveFolders && prepared.Folders.Count > 0) summary += $" · 新建 {prepared.Folders.Count:N0} 个文件夹";
            if (items.Any(item => item.Status == FileImportStatus.Duplicate)) summary += $" · 跳过重复 {items.Count(item => item.Status == FileImportStatus.Duplicate):N0} 项";
            if (items.Any(item => item.Status is FileImportStatus.Unavailable or FileImportStatus.Unsupported)) summary += $" · 失效或不支持 {items.Count(item => item.Status is FileImportStatus.Unavailable or FileImportStatus.Unsupported):N0} 项";
            ImportReport = new(items, summary);
            InputFeedback = ImportReport.Summary;
            CurrentFolderId = destination; files.RemoveAll(file => file.IsSample); CollectionChanged();
            return ImportReport;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return new([], "已取消转交，原列表保留。"); }
        finally
        {
            if (version == importGeneration) { importCancellation = null; IsLoading = false; NotifyFileState(); }
        }
    }
    public async Task RefreshReferencesAsync()
    {
        if (IsLoading || inputReader is null) return;
        using var cancellation = new CancellationTokenSource(); importCancellation = cancellation;
        var version = ++importGeneration;
        IsLoading = true; LoadingMessage = "正在检查文件引用…";
        try
        {
            var report = await inputReader.ReadAsync(SearchEntries.Select(file => file.FullPath).ToArray(), cancellation.Token);
            if (version != importGeneration || cancellation.IsCancellationRequested) return;
            var updates = report.Items.ToDictionary(item => item.Path, item =>
                (item.File ?? collection.Entries.First(entry => entry.File.FullPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase)).File,
                item.Status == FileImportStatus.Ready ? null : (string?)"失效：" + item.Reason), StringComparer.OrdinalIgnoreCase);
            collection.UpdateReferences(updates); CollectionChanged();
            InputFeedback = $"检查完成 · {updates.Values.Count(item => item.Item2 is not null):N0} 个失效路径，引用保留。";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (version == importGeneration) { importCancellation = null; IsLoading = false; } }
    }
    public async Task LoadCollectionAsync()
    {
        if (collectionStore is null) return;
        restoringCollection = true;
        IsLoading = true; LoadingMessage = "正在恢复软件文件夹和文件引用…";
        try
        {
            var loaded = await collectionStore.LoadAsync(CancellationToken.None);
            if (loaded.Snapshot is { } snapshot) InitializeCollection(snapshot);
            saveEnabled = loaded.CanSave;
            PersistenceMessage = loaded.Message ?? "软件文件夹和引用会自动保存。";
        }
        catch (Exception) { saveEnabled = false; PersistenceMessage = "无法恢复文件夹，本次暂停自动保存，原记录保留。"; }
        finally { restoringCollection = false; IsLoading = false; NotifyFileState(); }
    }
    private void QueueSave()
    {
        if (collectionStore is null || !saveEnabled || HasSamples) return;
        var version = ++dirtyVersion;
        saveCancellation?.Cancel(); saveCancellation = new();
        pendingSave = SaveAfterDelayAsync(version, saveCancellation.Token);
    }
    private async Task SaveAfterDelayAsync(int version, CancellationToken token)
    {
        try { await Task.Delay(400, token); await collectionStore!.SaveAsync(CollectionSnapshot, token); savedVersion = Math.Max(savedVersion, version);
            if (version == dirtyVersion) { PersistenceMessage = "软件文件夹和引用已自动保存。"; CanRetrySave = false; } }
        catch (OperationCanceledException) { }
        catch (Exception) { PersistenceMessage = "自动保存失败，请检查磁盘或权限后点击“重试保存”。"; CanRetrySave = true; }
    }
    public async Task FlushCollectionAsync()
    {
        saveCancellation?.Cancel(); await pendingSave;
        if (collectionStore is null || !saveEnabled || savedVersion == dirtyVersion || HasSamples) return;
        try { await collectionStore.SaveAsync(CollectionSnapshot, CancellationToken.None); savedVersion = dirtyVersion; PersistenceMessage = "软件文件夹和引用已自动保存。"; CanRetrySave = false; }
        catch (Exception) { PersistenceMessage = "文件夹保存失败，本次改动尚未保存。"; CanRetrySave = true; }
    }
}
