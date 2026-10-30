using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Features.Search;
using FileAssistant.App.Features.Search;

namespace FileAssistant.App.Features.Workspace;

/// <summary>Workspace presentation state. Sample actions never access the file system.</summary>
public partial class WorkspaceViewModel : ObservableObject
{
    private readonly List<WorkspaceFile> files = [];

    public ObservableCollection<WorkspaceFile> VisibleFiles { get; } = [];
    public IReadOnlyList<SearchFile> SearchEntries => files.Where(f => f.Source is not null).Select(f => f.Source!).ToArray();
    public bool HasSamples => files.Any(f => f.IsSample);
    public string BatchOrigin => HasSamples ? "示例文件 · 不读取本地文件" : "真实文件 · 加入批次不会移动原件";
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
            var item = new SearchResultItem(entry);
            files.Add(new(entry.Name, item.TypeLabel, entry.Size ?? -1, entry.Directory, item.ModifiedLabel, item.Glyph, SearchResultItem.Category(entry), entry));
            added++;
        }
        HasError = false;
        ApplyFilters();
        return added;
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectionSummary), nameof(PreviewName),
        nameof(PreviewType), nameof(PreviewSize), nameof(PreviewPath), nameof(PreviewDate), nameof(PreviewGlyph), nameof(PreviewOrigin))]
    public partial WorkspaceFile? SelectedFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    public partial int SelectionCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoResults), nameof(ShowList), nameof(HasFiles), nameof(CanEditList))]
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
    public bool ShowEmpty => !HasFiles && !IsLoading && !HasError;
    public bool ShowNoResults => HasFiles && VisibleFiles.Count == 0 && !IsLoading && !HasError;
    public bool ShowList => VisibleFiles.Count > 0 && !IsLoading && !HasError;
    public bool ShowEmptyQueue => !IsTaskExample;
    public string BatchSummary => HasFiles ? $"{files.Count} 项{(HasSamples ? "示例" : "真实文件")} · 已知大小 {WorkspaceFile.FormatSize(files.Where(file => file.Size >= 0).Sum(file => file.Size))}" : "集中查看文件，准备批量操作";
    public string ListSummary => $"{VisibleFiles.Count} / {files.Count} 项";
    public string SelectionSummary => SelectionCount == 0 ? "未选择文件" : $"已选 {SelectionCount} 项";
    public string TaskSummary => IsTaskExample ? "复制进度示例 · 62% · 未执行文件操作" : "暂无进行中的任务";
    public string PreviewName => SelectedFile?.Name ?? "选择一个文件";
    public string PreviewType => SelectedFile?.Type ?? string.Empty;
    public string PreviewSize => SelectedFile?.SizeLabel ?? string.Empty;
    public string PreviewPath => SelectedFile?.FullPath ?? string.Empty;
    public string PreviewDate => SelectedFile?.Modified ?? string.Empty;
    public string PreviewGlyph => SelectedFile?.Glyph ?? "\uE8A5";

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterIndexChanged(int value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadSamplesAsync(string? mode, CancellationToken cancellationToken)
    {
        if (files.Any(f => !f.IsSample)) return;
        HasError = false;
        IsLoading = true;
        try
        {
            // Intentional delay makes loading and cancellation reviewable.
            await Task.Delay(mode == "slow" ? 4000 : 700, cancellationToken);
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
            IsLoading = false;
            NotifyFileState();
        }
    }

    [RelayCommand]
    private void CancelLoading() => LoadSamplesCommand.Cancel();

    [RelayCommand]
    private void Clear()
    {
        LoadSamplesCommand.Cancel();
        files.Clear();
        SearchText = string.Empty;
        FilterIndex = 0;
        HasError = false;
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
        SelectedFile = null;
        SelectionCount = 0;
        VisibleFiles.Clear();
        var query = SearchText.Trim();
        foreach (var file in files.Where(file =>
            (query.Length == 0 || file.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                file.Directory.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (FilterIndex <= 0 || file.Category == FilterIndex)))
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
    }
}
