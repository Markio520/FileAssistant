using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public partial class SearchViewModel(IFileSearch search, Func<IReadOnlyList<SearchFile>> batch, Func<IReadOnlyList<SearchFile>, int> addToBatch, IFileInteraction? interaction = null) : ObservableObject
{
    private CancellationTokenSource? pending;
    private int generation;
    private bool active;
    private bool composing;
    private SearchRequest? completedRequest;
    private int fetched;
    public SearchColumnWidths ColumnWidths { get; } = new();

    [ObservableProperty] public partial string QueryText { get; set; } = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsFolderScope), nameof(ScopeLabel))] public partial int ScopeIndex { get; set; }
    [ObservableProperty] public partial string Folder { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IncludeSubfolders { get; set; } = true;
    [ObservableProperty] public partial bool MatchPath { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(KindLabel))] public partial int KindIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SizeLabel))] public partial int SizeIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DateLabel))] public partial int DateIndex { get; set; }
    [ObservableProperty] public partial int SortIndex { get; set; }
    [ObservableProperty] public partial ObservableCollection<SearchResultItem> Results { get; set; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanLoadMore), nameof(CanAdd), nameof(ShowEmpty))] public partial bool IsBusy { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(CanLoadMore))] public partial bool HasError { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;
    [ObservableProperty] public partial string Status { get; set; } = "正在准备文件列表…";
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyDescription))] public partial bool IsCancelled { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanLoadMore))] public partial long Total { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanAdd), nameof(SelectionLabel))] public partial int SelectionCount { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection), nameof(DetailName), nameof(DetailPath), nameof(DetailType), nameof(DetailSize), nameof(DetailModified), nameof(DetailGlyph))]
    public partial SearchResultItem? SelectedItem { get; set; }

    public bool IsFolderScope => ScopeIndex == 1;
    public string ScopeLabel => SearchFilterOptions.Scopes.First(item => item.Value == ScopeIndex).Label;
    public string KindLabel => SearchFilterOptions.Kinds.First(item => item.Value == KindIndex).Label;
    public string SizeLabel => SearchFilterOptions.Sizes.First(item => item.Value == SizeIndex).Label;
    public string DateLabel => SearchFilterOptions.Dates.First(item => item.Value == DateIndex).Label;
    public bool CanLoadMore => !IsBusy && !HasError && fetched < Total;
    public bool CanAdd => !IsBusy && !HasError && SelectionCount > 0;
    public bool HasSearchSource => !IsFolderScope || !string.IsNullOrWhiteSpace(Folder);
    public bool ShowEmpty => HasSearchSource && !IsBusy && !HasError && Results.Count == 0;
    public bool HasSelection => SelectedItem is not null;
    public string EmptyTitle => IsCancelled ? "搜索已取消" : "没有匹配结果";
    public string EmptyDescription => IsCancelled ? "已停止本次查找，可修改条件或刷新重新搜索。" : "调整关键词或筛选条件，也可以更换搜索范围。";
    public string SelectionLabel => SelectionCount == 0 ? "可多选文件加入批次" : $"已选 {SelectionCount} 项";
    public string DetailName => SelectedItem?.Name ?? "选择文件查看信息";
    public string DetailPath => SelectedItem?.FullPath ?? string.Empty;
    public string DetailType => SelectedItem?.TypeLabel ?? string.Empty;
    public string DetailSize => SelectedItem?.SizeLabel ?? string.Empty;
    public string DetailModified => SelectedItem?.ModifiedLabel ?? string.Empty;
    public string DetailGlyph => SelectedItem?.Glyph ?? "\uE721";

    partial void OnQueryTextChanged(string value) => Schedule();
    partial void OnScopeIndexChanged(int value) => Schedule();
    partial void OnFolderChanged(string value) => Schedule();
    partial void OnIncludeSubfoldersChanged(bool value) => Schedule();
    partial void OnMatchPathChanged(bool value) => Schedule();
    partial void OnKindIndexChanged(int value) => Schedule();
    partial void OnSizeIndexChanged(int value) => Schedule();
    partial void OnDateIndexChanged(int value) => Schedule();
    partial void OnSortIndexChanged(int value) => Schedule();

    public void Activate() { active = true; _ = RunAsync(false, false); }
    public void Deactivate() { active = false; Cancel(); }
    private void Schedule() { if (active && !composing) _ = RunAsync(false, true); }

    public void SetInputComposition(bool composing)
    {
        this.composing = composing;
        if (composing) Cancel();
        else Schedule();
    }

    [RelayCommand] private async Task RefreshAsync()
    {
        search.InvalidateFolder();
        if (ScopeIndex == 0) search.RetryIndexInitialization();
        await RunAsync(false, false);
    }
    [RelayCommand] private Task LoadMoreAsync() => CanLoadMore ? RunAsync(true, false) : Task.CompletedTask;
    [RelayCommand] private void Cancel()
    {
        if (pending is not null) { pending.Cancel(); Status = "已取消搜索 · 可修改条件或刷新"; }
    }
    [RelayCommand] private void ResetFilters()
    {
        QueryText = string.Empty; KindIndex = 0; SizeIndex = 0; DateIndex = 0; MatchPath = false;
    }

    public void AddSelection(IReadOnlyList<SearchResultItem> selection)
    {
        if (!CanAdd) return;
        var count = addToBatch(selection.Select(i => i.File).ToArray());
        Feedback = count > 0 ? $"已加入 {count} 项；可继续查找，或打开操作批次。" : "这些文件已在操作批次中。";
    }

    public void OpenFile(SearchResultItem item) => Interact(() => interaction!.Open(item.File));
    public void OpenLocation(SearchResultItem item) => Interact(() => interaction!.Reveal(item.File));
    public void CopySelection(IReadOnlyList<SearchResultItem> selection, bool namesOnly)
    {
        if (selection.Count == 0) return;
        Interact(() =>
        {
            interaction!.CopyText(string.Join(Environment.NewLine, selection.Select(item => namesOnly ? item.Name : item.FullPath)));
            Feedback = $"已复制 {selection.Count} 项的{(namesOnly ? "名称" : "完整路径")}";
        });
    }
    private void Interact(Action action)
    {
        Feedback = string.Empty;
        try { action(); }
        catch (Exception e) { Feedback = e is FileInteractionException ? e.Message : "无法完成此操作，请检查文件访问权限后重试。"; }
    }

    private SearchRequest Request() => new()
    {
        Text = QueryText, Scope = (SearchScope)ScopeIndex, Folder = Folder,
        IncludeSubfolders = IncludeSubfolders, MatchPath = MatchPath, Kind = (SearchKind)KindIndex,
        MinimumSize = SizeIndex switch { 2 => 1_048_576, 3 => 10_485_760, 4 => 104_857_600, 5 => 1_073_741_824, _ => null },
        MaximumSize = SizeIndex switch { 1 => 1_048_575, 2 => 10_485_759, _ => null },
        ModifiedFrom = DateIndex switch { 1 => DateTimeOffset.Now.Date, 2 => DateTimeOffset.Now.Date.AddDays(-6), 3 => DateTimeOffset.Now.Date.AddDays(-29), _ => null },
        Sort = (SearchSort)(SortIndex / 2), Descending = SortIndex % 2 == 1
    };

    private async Task RunAsync(bool append, bool debounce)
    {
        pending?.Cancel();
        using var cancellation = new CancellationTokenSource();
        pending = cancellation;
        var version = ++generation;
        var token = cancellation.Token;
        var request = append && completedRequest is not null ? completedRequest with { Offset = fetched } : Request();
        if (!append)
        {
            Results = []; fetched = 0; Total = 0; SelectedItem = null; SelectionCount = 0; completedRequest = null;
        }
        HasError = false; IsCancelled = false; Message = string.Empty; Feedback = string.Empty; IsBusy = true;
        if (!HasSearchSource)
        {
            pending = null; IsBusy = false; Status = "选择文件夹以查看内容";
            OnPropertyChanged(nameof(ShowEmpty));
            return;
        }
        Status = "正在搜索…";
        try
        {
            if (debounce) await Task.Delay(150, token);
            var stopwatch = Stopwatch.StartNew();
            var progress = new Progress<SearchProgress>(p =>
            {
                if (version == generation && !token.IsCancellationRequested) Status = p.Message ?? $"正在扫描 · 已发现 {p.Examined:N0} 项";
            });
            var page = await search.SearchAsync(request, batch(), progress, token);
            if (version != generation || token.IsCancellationRequested) return;
            var paths = Results.Select(i => i.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in page.Items)
                if (paths.Add(file.FullPath)) Results.Add(new(file, ColumnWidths));
            fetched += page.Items.Count;
            Total = page.Total;
            completedRequest = request with { Offset = 0 };
            Status = $"{page.Source} · {Total:N0} 项 · 已载入 {Results.Count:N0} · {stopwatch.ElapsedMilliseconds:N0} ms";
            if (page.Skipped > 0) Status += $" · {page.Skipped:N0} 项无法读取";
            if (page.Items.Count == 0 && fetched < Total) { fetched = (int)Math.Min(Total, int.MaxValue); Feedback = "索引在查询期间发生变化，请刷新获取最新结果。"; }
        }
        catch (OperationCanceledException) { if (version == generation) { IsCancelled = true; Status = "已取消搜索 · 可修改条件或刷新"; } }
        catch (Exception e)
        {
            if (version != generation) return;
            HasError = true;
            Message = e is SearchException ? e.Message : "无法完成搜索，请检查目录权限后重试。";
            Status = "搜索未完成";
        }
        finally
        {
            if (version == generation)
            {
                pending = null; IsBusy = false;
                OnPropertyChanged(nameof(CanLoadMore)); OnPropertyChanged(nameof(ShowEmpty));
            }
        }
    }
}
