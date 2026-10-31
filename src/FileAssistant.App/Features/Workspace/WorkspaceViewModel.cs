using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Features.Search;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.FileCollections;

namespace FileAssistant.App.Features.Workspace;

/// <summary>Workspace presentation state. Sample actions never access the file system.</summary>
public partial class WorkspaceViewModel : ObservableObject
{
    private readonly List<WorkspaceFile> files = [];
    private readonly IFileInputReader? inputReader;
    private CancellationTokenSource? importCancellation;
    private int importGeneration;
    private IReadOnlyList<WorkspaceFile> selectedFiles = [];

    public WorkspaceViewModel(IFileInputReader? inputReader = null, bool isStaging = true)
        : this(inputReader, isStaging ? WorkspaceInputPurpose.Staging : WorkspaceInputPurpose.Copy) { }

    public WorkspaceViewModel(IFileInputReader? inputReader, WorkspaceInputPurpose purpose, IFileCollectionStore? store = null)
    { this.inputReader = inputReader; Purpose = purpose; collectionStore = store; }

    public WorkspaceInputPurpose Purpose { get; }
    public bool IsStaging => Purpose == WorkspaceInputPurpose.Staging;
    public bool IsFunctionInput => !IsStaging;
    public string Title => Purpose switch { WorkspaceInputPurpose.Staging => "文件暂存区", WorkspaceInputPurpose.Conversion => "格式转换", _ => "复制与整理" };
    public string EmptyTitle => CurrentFolderId != Guid.Empty ? "这个软件文件夹还没有文件" : IsStaging ? "暂存区还没有文件" : "还没有待处理的文件";
    public string InputDescription => IsStaging ? "选择、拖入或粘贴文件，稍后交给需要的功能。" : "直接添加文件，或从暂存区选择文件导入。";
    public bool CanReceiveFiles => !IsLoading;
    public bool CanCancelLoading => IsLoading && !restoringCollection;
    public bool CanExportSelection => !IsLoading && !HasSamples && SelectionCount > 0;
    public bool CanExportAll => !IsLoading && !HasSamples && (ScopeEntries.Count > 0 || collection.Folders.Any(folder => folder.ParentId == CurrentFolderId));
    public string SendSelectedLabel => $"选中 {SelectionCount:N0} 项 → 复制与整理";
    public string SendAllLabel => $"当前文件夹全部 {ScopeEntries.Count:N0} 项 → 复制与整理";
    public string SendSelectedToConversionLabel => $"选中 {SelectionCount:N0} 项 → 格式转换";
    public string SendAllToConversionLabel => $"当前文件夹全部 {ScopeEntries.Count:N0} 项 → 格式转换";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasInputFeedback), nameof(HasFooterMessage))] public partial string InputFeedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImportDetails))] public partial FileImportReport ImportReport { get; set; } = FileImportReport.Empty;
    [ObservableProperty] public partial string LoadingMessage { get; set; } = string.Empty;
    public bool HasInputFeedback => InputFeedback.Length > 0;
    public bool HasImportDetails => ImportReport.Items.Count > 0;

    public ObservableCollection<WorkspaceFile> VisibleFiles { get; } = [];
    public IReadOnlyList<SearchFile> SearchEntries => collection.Resolve(Guid.Empty).Select(entry => entry.File).ToArray();
    public IReadOnlyList<SearchFile> SelectedEntries => collection.ResolveSelection(SelectedFolderIds, SelectedEntryIds).Select(entry => entry.File).ToArray();

    public void SetSelection(IReadOnlyList<WorkspaceFile> selection, WorkspaceFile? current = null)
    {
        selectedFiles = selection.ToArray();
        SelectionCount = selectedFiles.Count;
        SelectedFile = current ?? selectedFiles.LastOrDefault();
        OnPropertyChanged(nameof(SelectedEntries));
        OnPropertyChanged(nameof(EffectiveEntries));
        OnPropertyChanged(nameof(CanRenameFolder));
    }
    public bool HasSamples => files.Any(f => f.IsSample);
    public string BatchOrigin => HasSamples ? "示例文件 · 不读取本地文件" : IsStaging ? "暂存引用 · 不改动原件" : "独立输入 · 不自动加入暂存区";
    public string PreviewOrigin => SelectedFile?.IsSample != false ? "示例信息 · 未读取文件内容" : "真实文件信息 · 未加载内容预览";

    public int AddSearchFiles(IReadOnlyList<SearchFile> entries)
    {
        var before = CollectionSnapshot;
        LoadSamplesCommand.Cancel();
        files.RemoveAll(f => f.IsSample);
        var added = collection.AddFiles(CurrentFolderId, entries);
        RememberChange(before, "加入文件");
        HasError = false;
        CollectionChanged();
        return added;
    }

    public Task<FileImportReport> ImportPathsAsync(IReadOnlyList<string> paths) => ImportPathsAsync(paths, CurrentFolderId);
    public async Task<FileImportReport> ImportPathsAsync(IReadOnlyList<string> paths, Guid destination)
    {
        if (IsLoading) return new([], InputFeedback = "正在添加文件，请完成或取消后重试。");
        if (paths.Count == 0) return new([], "没有选择文件。");
        if (inputReader is null) throw new InvalidOperationException("File input reader is not configured.");
        using var cancellation = new CancellationTokenSource();
        importCancellation = cancellation;
        var version = ++importGeneration;
        IsLoading = true; HasError = false;
        LoadingMessage = $"正在检查 {paths.Count:N0} 个文件或文件夹…";
        try
        {
            var report = await inputReader.ReadAsync(paths, cancellation.Token);
            if (version != importGeneration || cancellation.IsCancellationRequested) return new([], "已取消添加，列表保持不变。");
            if (!collection.HasFolder(destination)) return new([], InputFeedback = "目标软件文件夹已不存在，添加已取消。");
            var known = collection.Entries.Where(entry => entry.FolderId == destination).Select(entry => entry.File.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var items = report.Items.Select(item => item.Status != FileImportStatus.Ready || item.File is null ? item
                : known.Add(item.File.FullPath) ? item with { Status = FileImportStatus.Added }
                : item with { Status = FileImportStatus.Duplicate, Reason = "当前列表已包含此路径。" }).ToArray();
            var added = items.Where(item => item.Status == FileImportStatus.Added).Select(item => item.File!).ToArray();
            if (added.Length > 0) { var before = CollectionSnapshot; collection.AddFiles(destination, added); RememberChange(before, "加入文件"); files.RemoveAll(file => file.IsSample); CollectionChanged(); }
            ImportReport = new(items);
            InputFeedback = ImportReport.Summary;
            return ImportReport;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { return new([], "已取消添加，列表保持不变。"); }
        catch (Exception)
        {
            var report = new FileImportReport([], "读取文件失败，原列表已保留。请检查路径和访问权限后重试。");
            if (version == importGeneration) InputFeedback = report.Summary;
            return report;
        }
        finally
        {
            if (version == importGeneration) { importCancellation = null; IsLoading = false; }
        }
    }

    public Task<FileImportReport> ImportFilesAsync(IReadOnlyList<SearchFile> entries)
        => ImportPathsAsync(entries.Select(entry => entry.FullPath).ToArray());

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ShowSelectionHint), nameof(SelectionSummary), nameof(PreviewName),
        nameof(PreviewType), nameof(PreviewSize), nameof(PreviewPath), nameof(PreviewDate), nameof(PreviewGlyph), nameof(PreviewOrigin), nameof(PreviewFile), nameof(PreviewStatus))]
    public partial WorkspaceFile? SelectedFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary), nameof(CanExportSelection), nameof(SendSelectedLabel), nameof(SendSelectedToConversionLabel))]
    public partial int SelectionCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoResults), nameof(ShowList), nameof(HasFiles), nameof(CanEditList), nameof(CanReceiveFiles), nameof(CanExportSelection), nameof(CanExportAll), nameof(CanGoUp), nameof(CanRenameFolder), nameof(CanCancelLoading))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadingCommand))]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoResults), nameof(ShowList))]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsQueueExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskSummary), nameof(ShowEmptyQueue))]
    public partial bool IsTaskExample { get; set; }

    public bool HasFiles => files.Count > 0 || collection.Folders.Count > 0;
    public bool CanEditList => HasFiles && !IsLoading;
    public bool HasSelection => SelectedFile is not null;
    public bool ShowSelectionHint => !HasSelection;
    public bool ShowEmpty => !HasCurrentItems && !IsLoading && !HasError;
    public bool ShowNoResults => HasCurrentItems && VisibleFiles.Count == 0 && !IsLoading && !HasError;
    public bool ShowList => VisibleFiles.Count > 0 && !IsLoading && !HasError;
    public bool ShowEmptyQueue => !IsTaskExample;
    public string BatchSummary => HasFiles ? collection.Folders.Count > 0
        ? $"当前文件夹含 {ScopeEntries.Count:N0} 个文件引用 · 全部 {SearchEntries.Count:N0} 个不同路径 · {collection.Folders.Count:N0} 个软件文件夹"
        : $"{files.Count} 项{(HasSamples ? "示例" : "文件或文件夹")} · 已知大小 {WorkspaceFile.FormatSize(files.Where(file => file.Size >= 0).Sum(file => file.Size))}" : InputDescription;
    public string ListSummary => $"{VisibleFiles.Count} / {CurrentRows().Count()} 项";
    public string SelectionSummary => SelectionCount == 0 ? HasSamples ? "未勾选示例" : $"未勾选 · 当前文件夹及子文件夹共 {ScopeEntries.Count:N0} 项"
        : SelectedFolderIds.Count > 0 ? SelectedEntries.Count == 0 ? "所选文件夹为空"
            : $"已选 {SelectedFolderIds.Count:N0} 个文件夹" + (SelectionCount > SelectedFolderIds.Count ? $"、{SelectionCount - SelectedFolderIds.Count:N0} 个文件" : "") + $" · 实际 {SelectedEntries.Count:N0} 项（含子文件夹，已去重）"
        : $"已选 {SelectionCount:N0} 项";
    public string TaskSummary => IsTaskExample ? "复制进度示例 · 62% · 未执行文件操作" : "暂无进行中的任务";
    public string PreviewName => SelectedFile?.Name ?? "选择一个文件";
    public string PreviewType => SelectedFile?.Type ?? string.Empty;
    public string PreviewSize => SelectedFile?.SizeLabel ?? string.Empty;
    public string PreviewPath => SelectedFile?.FullPath ?? string.Empty;
    public string PreviewDate => SelectedFile?.Modified ?? string.Empty;
    public string PreviewStatus => SelectedFile?.ReferenceError ?? (SelectedFile?.IsCollectionFolder == true ? "软件内的文件引用分组。磁盘原件留在原位置。" : string.Empty);
    public string PreviewGlyph => SelectedFile?.Glyph ?? "\uE8A5";
    public SearchFile? PreviewFile => SelectedFile?.ReferenceError is null ? SelectedFile?.Source : null;

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterIndexChanged(int value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadSamplesAsync(string? mode, CancellationToken cancellationToken)
    {
        if (IsLoading || collection.Folders.Count > 0 || files.Any(f => !f.IsSample)) return;
        var version = ++importGeneration;
        HasError = false;
        IsLoading = true;
        LoadingMessage = "正在载入示例文件…";
        try
        {
            // Intentional delay makes loading and cancellation reviewable.
            await Task.Delay(mode == "slow" ? 4000 : 700, cancellationToken);
            if (version != importGeneration) return;
            files.Clear();
            files.AddRange(WorkspaceSamples.Create());
            SearchText = string.Empty;
            FilterIndex = 0;
            ApplyFilters();
        }
        catch (OperationCanceledException)
        {
            // Cancellation leaves the existing batch untouched.
        }
        finally
        {
            if (version == importGeneration) { IsLoading = false; NotifyFileState(); }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelLoading))]
    private void CancelLoading()
    {
        // Restoring persisted references must finish before edits are accepted.
        if (restoringCollection) return;
        ++importGeneration;
        importCancellation?.Cancel(); importCancellation = null;
        LoadSamplesCommand.Cancel();
        IsLoading = false;
        InputFeedback = "已取消添加，原列表已保留。";
    }

    [RelayCommand]
    private void Clear()
    {
        if (restoringCollection) return;
        var before = CollectionSnapshot;
        CancelLoading();
        files.Clear();
        collection.ClearFiles(CurrentFolderId);
        RememberChange(before, "清空当前文件夹");
        SearchText = string.Empty;
        FilterIndex = 0;
        HasError = false;
        InputFeedback = string.Empty; ImportReport = FileImportReport.Empty;
        CollectionChanged();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = string.Empty;
        FilterIndex = 0;
    }

    [RelayCommand]
    private void ShowError()
    {
        LoadSamplesCommand.Cancel();
        HasError = true;
    }

    [RelayCommand]
    private void DismissError() => HasError = false;

    [RelayCommand]
    private void ShowTaskExample()
    {
        IsTaskExample = true;
        IsQueueExpanded = true;
    }

    [RelayCommand]
    private void CloseTaskExample() => IsTaskExample = false;

    public void RemoveFromList(IEnumerable<WorkspaceFile> selection)
    {
        var before = CollectionSnapshot;
        var chosen = selection.ToArray();
        foreach (var file in chosen.Where(file => file.IsSample)) files.Remove(file);
        collection.Remove(chosen.Where(file => file.FolderId is not null).Select(file => file.FolderId!.Value), chosen.Where(file => file.EntryId is not null).Select(file => file.EntryId!.Value));
        RememberChange(before, "移出列表");
        CollectionChanged();
    }

    private void ApplyFilters()
    {
        SetSelection([]);
        VisibleFiles.Clear();
        var query = SearchText.Trim();
        var typeFilter = SearchRules.Compile(new SearchRequest { Kind = (SearchKind)FilterIndex });
        foreach (var file in CurrentRows().Where(file =>
            (query.Length == 0 || file.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                file.Directory.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (file.IsCollectionFolder ? FilterIndex is (int)SearchKind.All or (int)SearchKind.Folders : typeFilter.Matches(file.Source ?? new SearchFile(file.FullPath, false, file.Size, null)))))
        {
            VisibleFiles.Add(file);
        }

        NotifyFileState();
    }

    private void NotifyFileState()
    {
        OnPropertyChanged(nameof(HasSamples));
        OnPropertyChanged(nameof(BatchOrigin));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(CanEditList));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(BatchSummary));
        OnPropertyChanged(nameof(ListSummary));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanExportSelection));
        OnPropertyChanged(nameof(CanExportAll));
        OnPropertyChanged(nameof(SendAllLabel));
        OnPropertyChanged(nameof(SendAllToConversionLabel));
    }
}
