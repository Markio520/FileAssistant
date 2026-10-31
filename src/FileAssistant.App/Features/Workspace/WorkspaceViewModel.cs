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

    public WorkspaceViewModel(IFileInputReader? inputReader, WorkspaceInputPurpose purpose)
    { this.inputReader = inputReader; Purpose = purpose; }

    public WorkspaceInputPurpose Purpose { get; }
    public bool IsStaging => Purpose == WorkspaceInputPurpose.Staging;
    public bool IsFunctionInput => !IsStaging;
    public string Title => Purpose switch { WorkspaceInputPurpose.Staging => "文件暂存区", WorkspaceInputPurpose.Conversion => "格式转换", _ => "复制与整理" };
    public string EmptyTitle => IsStaging ? "暂存区还没有文件" : "还没有待处理的文件";
    public string InputDescription => IsStaging ? "选择、拖入或粘贴文件，稍后交给需要的功能。" : "直接添加文件，或从暂存区选择文件导入。";
    public bool CanReceiveFiles => !IsLoading;
    public bool CanExportSelection => !IsLoading && !HasSamples && SelectionCount > 0;
    public bool CanExportAll => !IsLoading && SearchEntries.Count > 0;
    public string SendSelectedLabel => $"选中 {SelectionCount:N0} 项 → 复制与整理";
    public string SendAllLabel => $"全部 {SearchEntries.Count:N0} 项 → 复制与整理";
    public string SendSelectedToConversionLabel => $"选中 {SelectionCount:N0} 项 → 格式转换";
    public string SendAllToConversionLabel => $"全部 {SearchEntries.Count:N0} 项 → 格式转换";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasInputFeedback))] public partial string InputFeedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImportDetails))] public partial FileImportReport ImportReport { get; set; } = FileImportReport.Empty;
    [ObservableProperty] public partial string LoadingMessage { get; set; } = string.Empty;
    public bool HasInputFeedback => InputFeedback.Length > 0;
    public bool HasImportDetails => ImportReport.Items.Count > 0;

    public ObservableCollection<WorkspaceFile> VisibleFiles { get; } = [];
    public IReadOnlyList<SearchFile> SearchEntries => files.Where(f => f.Source is not null).Select(f => f.Source!).ToArray();
    public IReadOnlyList<SearchFile> SelectedEntries => selectedFiles.Where(f => f.Source is not null).Select(f => f.Source!).ToArray();

    public void SetSelection(IReadOnlyList<WorkspaceFile> selection, WorkspaceFile? current = null)
    {
        selectedFiles = selection.ToArray();
        SelectionCount = selectedFiles.Count;
        SelectedFile = current ?? selectedFiles.LastOrDefault();
        OnPropertyChanged(nameof(SelectedEntries));
    }
    public bool HasSamples => files.Any(f => f.IsSample);
    public string BatchOrigin => HasSamples ? "示例文件 · 不读取本地文件" : IsStaging ? "暂存引用 · 不改动原件" : "独立输入 · 不自动加入暂存区";
    public string PreviewOrigin => SelectedFile?.IsSample != false ? "示例信息 · 未读取文件内容" : "真实文件信息 · 未加载内容预览";

    public int AddSearchFiles(IReadOnlyList<SearchFile> entries)
    {
        LoadSamplesCommand.Cancel();
        files.RemoveAll(f => f.IsSample);
        var known = files.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var entry in entries)
        {
            if (!known.Add(entry.FullPath)) continue;
            files.Add(WorkspaceFile.FromSource(entry));
            added++;
        }
        HasError = false;
        ApplyFilters();
        return added;
    }

    public async Task<FileImportReport> ImportPathsAsync(IReadOnlyList<string> paths)
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
            var known = SearchEntries.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var items = report.Items.Select(item => item.Status != FileImportStatus.Ready || item.File is null ? item
                : known.Add(item.File.FullPath) ? item with { Status = FileImportStatus.Added }
                : item with { Status = FileImportStatus.Duplicate, Reason = "当前列表已包含此路径。" }).ToArray();
            var added = items.Where(item => item.Status == FileImportStatus.Added).Select(item => item.File!).ToArray();
            if (added.Length > 0) AddSearchFiles(added);
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
        nameof(PreviewType), nameof(PreviewSize), nameof(PreviewPath), nameof(PreviewDate), nameof(PreviewGlyph), nameof(PreviewOrigin), nameof(PreviewFile))]
    public partial WorkspaceFile? SelectedFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary), nameof(CanExportSelection), nameof(SendSelectedLabel), nameof(SendSelectedToConversionLabel))]
    public partial int SelectionCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoResults), nameof(ShowList), nameof(HasFiles), nameof(CanEditList), nameof(CanReceiveFiles), nameof(CanExportSelection), nameof(CanExportAll))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoResults), nameof(ShowList))]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsQueueExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskSummary), nameof(ShowEmptyQueue))]
    public partial bool IsTaskExample { get; set; }

    public bool HasFiles => files.Count > 0;
    public bool CanEditList => HasFiles && !IsLoading;
    public bool HasSelection => SelectedFile is not null;
    public bool ShowSelectionHint => !HasSelection;
    public bool ShowEmpty => !HasFiles && !IsLoading && !HasError;
    public bool ShowNoResults => HasFiles && VisibleFiles.Count == 0 && !IsLoading && !HasError;
    public bool ShowList => VisibleFiles.Count > 0 && !IsLoading && !HasError;
    public bool ShowEmptyQueue => !IsTaskExample;
    public string BatchSummary => HasFiles ? $"{files.Count} 项{(HasSamples ? "示例" : "文件或文件夹")} · 已知大小 {WorkspaceFile.FormatSize(files.Where(file => file.Size >= 0).Sum(file => file.Size))}" : InputDescription;
    public string ListSummary => $"{VisibleFiles.Count} / {files.Count} 项";
    public string SelectionSummary => SelectionCount == 0 ? "未选择文件" : $"已选 {SelectionCount} 项";
    public string TaskSummary => IsTaskExample ? "复制进度示例 · 62% · 未执行文件操作" : "暂无进行中的任务";
    public string PreviewName => SelectedFile?.Name ?? "选择一个文件";
    public string PreviewType => SelectedFile?.Type ?? string.Empty;
    public string PreviewSize => SelectedFile?.SizeLabel ?? string.Empty;
    public string PreviewPath => SelectedFile?.FullPath ?? string.Empty;
    public string PreviewDate => SelectedFile?.Modified ?? string.Empty;
    public string PreviewGlyph => SelectedFile?.Glyph ?? "\uE8A5";
    public SearchFile? PreviewFile => SelectedFile?.Source;

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterIndexChanged(int value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadSamplesAsync(string? mode, CancellationToken cancellationToken)
    {
        if (IsLoading || files.Any(f => !f.IsSample)) return;
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

    [RelayCommand]
    private void CancelLoading()
    {
        ++importGeneration;
        importCancellation?.Cancel(); importCancellation = null;
        LoadSamplesCommand.Cancel();
        IsLoading = false;
        InputFeedback = "已取消添加，原列表已保留。";
    }

    [RelayCommand]
    private void Clear()
    {
        CancelLoading();
        files.Clear();
        SearchText = string.Empty;
        FilterIndex = 0;
        HasError = false;
        InputFeedback = string.Empty; ImportReport = FileImportReport.Empty;
        ApplyFilters();
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
        foreach (var file in selection.ToArray())
        {
            files.Remove(file);
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        SetSelection([]);
        VisibleFiles.Clear();
        var query = SearchText.Trim();
        var typeFilter = SearchRules.Compile(new SearchRequest { Kind = (SearchKind)FilterIndex });
        foreach (var file in files.Where(file =>
            (query.Length == 0 || file.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                file.Directory.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            typeFilter.Matches(file.Source ?? new SearchFile(file.FullPath, false, file.Size, null))))
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
        OnPropertyChanged(nameof(CanExportSelection));
        OnPropertyChanged(nameof(CanExportAll));
        OnPropertyChanged(nameof(SendAllLabel));
        OnPropertyChanged(nameof(SendAllToConversionLabel));
    }
}
