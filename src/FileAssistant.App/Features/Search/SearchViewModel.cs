using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;
using FileAssistant.Core.Features.FileInput;

namespace FileAssistant.App.Features.Search;

public partial class SearchViewModel : ObservableObject
{
    private readonly IFileSearch search;
    private readonly Func<IReadOnlyList<SearchFile>> batch;
    private readonly Func<IReadOnlyList<SearchFile>, Task<FileImportReport>> addToStaging;
    private readonly IFileInteraction? interaction;
    private readonly ISettingsStore? settings;

    public SearchViewModel(IFileSearch search, Func<IReadOnlyList<SearchFile>> batch,
        Func<IReadOnlyList<SearchFile>, Task<FileImportReport>> addToStaging, IFileInteraction? interaction = null, ISettingsStore? settings = null)
    {
        this.search = search; this.batch = batch; this.addToStaging = addToStaging;
        this.interaction = interaction; this.settings = settings;
        ColumnWidths.PropertyChanged += (_, _) => { if (ColumnWidths.HasUserWidths) QueuePreferencesSave(); };
    }

    private CancellationTokenSource? pending;
    private int generation;
    private bool active;
    private bool composing;
    private bool needsSearch = true;
    private SearchRequest? completedRequest;
    private IReadOnlyList<SearchFile>? completedBatch;
    private int fetched;
    public SearchColumnWidths ColumnWidths { get; } = new();

    [ObservableProperty] public partial string QueryText { get; set; } = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsFolderScope), nameof(ScopeLabel))] public partial int ScopeIndex { get; set; }
    [ObservableProperty] public partial string Folder { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IncludeSubfolders { get; set; } = true;
    [ObservableProperty] public partial bool MatchPath { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(KindLabel))] public partial int KindIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SizeLabel), nameof(IsCustomSize))] public partial int SizeIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DateLabel), nameof(IsCustomDate))] public partial int DateIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NameSortGlyph), nameof(PathSortGlyph), nameof(SizeSortGlyph), nameof(ModifiedSortGlyph),
        nameof(NameSortDescription), nameof(PathSortDescription), nameof(SizeSortDescription), nameof(ModifiedSortDescription))] public partial int SortIndex { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowSearchError), nameof(ShowInlineError), nameof(ResultCountLabel))] public partial ObservableCollection<SearchResultItem> Results { get; set; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanLoadMore), nameof(CanAdd), nameof(CanUseResults), nameof(ShowEmpty), nameof(ResultCountLabel))] public partial bool IsBusy { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanUseResults), nameof(CanAdd))] public partial bool IsLoadingMore { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(CanLoadMore), nameof(CanAdd), nameof(CanUseResults), nameof(ShowSearchError), nameof(ShowInlineError))] public partial bool HasError { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowInlineError), nameof(InlineErrorTitle), nameof(LoadMoreLabel))] public partial bool HasPageError { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;
    [ObservableProperty] public partial string Status { get; set; } = "正在准备文件列表…";
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanAdd))] public partial bool IsAddingToStaging { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyDescription))] public partial bool IsCancelled { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanLoadMore), nameof(ResultCountLabel))] public partial long Total { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanAdd), nameof(ResultCountLabel))] public partial int SelectionCount { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection), nameof(DetailName), nameof(DetailPath), nameof(DetailType), nameof(DetailSize), nameof(DetailModified), nameof(DetailGlyph))]
    public partial SearchResultItem? SelectedItem { get; set; }

    public bool IsFolderScope => ScopeIndex == 1;
    public string ScopeLabel => SearchFilterOptions.Scopes.First(item => item.Value == ScopeIndex).Label;
    public string KindLabel => SearchFilterOptions.Kinds.First(item => item.Value == KindIndex).Label;
    public string SizeLabel => SearchFilterOptions.Sizes.First(item => item.Value == SizeIndex).Label;
    public string DateLabel => SearchFilterOptions.Dates.First(item => item.Value == DateIndex).Label;
    public bool CanUseResults => (!IsBusy || IsLoadingMore) && !HasError && completedRequest is not null && completedRequest == Request();
    public bool CanLoadMore => !IsBusy && CanUseResults && fetched < Total;
    public bool CanAdd => CanUseResults && SelectionCount > 0 && !IsAddingToStaging;
    public bool ShowSearchError => HasError && Results.Count == 0;
    public bool ShowInlineError => (HasError || HasPageError) && Results.Count > 0;
    public string InlineErrorTitle => HasPageError ? "后续结果加载失败，已加载文件仍可使用" : "搜索未完成，列表保留上次结果";
    public string LoadMoreLabel => HasPageError ? "重试加载" : "加载更多";
    public bool HasSearchSource => !IsFolderScope || !string.IsNullOrWhiteSpace(Folder);
    public bool ShowEmpty => HasSearchSource && !IsBusy && !HasError && !HasFilterError && Results.Count == 0;
    public bool HasSelection => SelectedItem is not null;
    public string EmptyTitle => IsCancelled ? "搜索已取消" : "没有匹配结果";
    public string EmptyDescription => IsCancelled ? "已停止本次查找，可修改条件或刷新重新搜索。" : "调整关键词或筛选条件，也可以更换搜索范围。";
    public string ResultCountLabel => (completedRequest is not null && !CanUseResults ? "上次结果 · " : string.Empty)
        + $"匹配 {(completedRequest is null ? "—" : Total.ToString("N0"))} 项 · 已加载 {Results.Count:N0} 项 · 已选 {SelectionCount:N0} 项";
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
    partial void OnSortIndexChanged(int value) { Schedule(); QueuePreferencesSave(); }

    public void Activate()
    {
        active = true;
        if (ScopeIndex == (int)SearchScope.Batch && completedBatch is not null && !completedBatch.SequenceEqual(batch())) needsSearch = true;
        if (needsSearch && !composing) _ = RunAsync(false, false);
    }
    public void Deactivate()
    {
        active = false;
        if (pending is null) return;
        // Resume an interrupted replacement on return; a cancelled next page can be requested again.
        needsSearch = !IsLoadingMore;
        Cancel();
    }
    private void Schedule()
    {
        needsSearch = true;
        if (updatingConditions) return;
        PresetIndex = -1;
        OnPropertyChanged(nameof(FilterError)); OnPropertyChanged(nameof(HasFilterError));
        NotifyResultActions();
        if (active && !composing) _ = RunAsync(false, true);
    }

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
    [RelayCommand] private Task RetryAsync() => HasError ? RefreshAsync() : LoadMoreAsync();
    [RelayCommand] private void Cancel()
    {
        if (pending is null) return;
        ++generation;
        pending.Cancel();
        pending = null;
        IsCancelled = true;
        Status = IsLoadingMore ? "已停止加载，已加载文件仍可使用" : Results.Count > 0
            ? "搜索已取消 · 列表保留上次结果，可刷新重试" : "已取消搜索 · 可修改条件或刷新";
        IsBusy = false;
        IsLoadingMore = false;
    }
    [RelayCommand] private void ResetFilters()
    {
        updatingConditions = true;
        QueryText = string.Empty; KindIndex = 0; SizeIndex = 0; DateIndex = 0; MatchPath = false;
        CustomMinimum = string.Empty; CustomMaximum = string.Empty; CustomDateFrom = null; CustomDateThrough = null;
        updatingConditions = false;
        Schedule();
    }

    public async Task AddSelectionAsync(IReadOnlyList<SearchResultItem> selection)
    {
        if (!CanAdd || selection.Count == 0 || selection.Any(item => !Results.Contains(item))) return;
        IsAddingToStaging = true;
        Feedback = "正在检查文件并加入暂存区…";
        try
        {
            var report = await addToStaging(selection.Select(i => i.File).ToArray());
            Feedback = report.Summary + (report.Items.Count > 0 ? "；打开文件暂存区可查看明细。" : string.Empty);
        }
        catch (Exception) { Feedback = "无法加入暂存区，请检查文件路径后重试。"; }
        finally { IsAddingToStaging = false; }
    }

    public void OpenFile(SearchResultItem item) { if (CanUseResults && Results.Contains(item)) Interact(() => interaction!.Open(item.File)); }
    public void OpenLocation(SearchResultItem item) { if (CanUseResults && Results.Contains(item)) Interact(() => interaction!.Reveal(item.File)); }
    public void CopySelection(IReadOnlyList<SearchResultItem> selection, bool namesOnly)
    {
        if (!CanUseResults || selection.Count == 0 || selection.Any(item => !Results.Contains(item))) return;
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

    private SearchRequest? Request()
    {
        if (!TryGetCustomRanges(out var minimum, out var maximum, out var from, out var before, out _)) return null;
        return new()
        {
            Text = QueryText, Scope = (SearchScope)ScopeIndex, Folder = Folder,
            IncludeSubfolders = IncludeSubfolders, MatchPath = MatchPath, Kind = (SearchKind)KindIndex,
            MinimumSize = SizeIndex switch { 2 => 1_048_576, 3 => 10_485_760, 4 => 104_857_600, 5 => 1_073_741_824, 6 => minimum, _ => null },
            MaximumSize = SizeIndex switch { 1 => 1_048_575, 2 => 10_485_759, 6 => maximum, _ => null },
            ModifiedFrom = DateIndex switch { 1 => DateTimeOffset.Now.Date, 2 => DateTimeOffset.Now.Date.AddDays(-6), 3 => DateTimeOffset.Now.Date.AddDays(-29), 4 => from, _ => null },
            ModifiedBefore = IsCustomDate ? before : null,
            Sort = (SearchSort)(SortIndex / 2), Descending = SortIndex % 2 == 1
        };
    }

    private async Task RunAsync(bool append, bool debounce)
    {
        pending?.Cancel();
        using var cancellation = new CancellationTokenSource();
        pending = cancellation;
        var version = ++generation;
        var token = cancellation.Token;
        var request = append && completedRequest is not null ? completedRequest with { Offset = fetched } : Request();
        needsSearch = false;
        HasError = false; HasPageError = false; IsCancelled = false; Message = string.Empty; Feedback = string.Empty;
        IsLoadingMore = append; IsBusy = true;
        if (request is null)
        {
            pending = null; IsBusy = false; IsLoadingMore = false;
            Status = Results.Count > 0 ? "请修正筛选条件 · 列表保留上次结果" : "请填写有效的筛选区间";
            NotifyResultActions(); OnPropertyChanged(nameof(ShowEmpty));
            return;
        }
        if (!HasSearchSource)
        {
            Results = []; fetched = 0; Total = 0; SelectedItem = null; SelectionCount = 0; completedRequest = null; completedBatch = null;
            pending = null; IsBusy = false; Status = "选择文件夹以查看内容";
            OnPropertyChanged(nameof(ShowEmpty));
            return;
        }
        Status = append ? "正在加载更多，已加载文件仍可使用…" : Results.Count > 0 ? "正在更新搜索 · 列表暂时显示上次结果…" : "正在搜索…";
        try
        {
            if (debounce) await Task.Delay(150, token);
            var stopwatch = Stopwatch.StartNew();
            var progress = new ThrottledSearchProgress(new Progress<SearchProgress>(p =>
            {
                if (version == generation && !token.IsCancellationRequested && IsBusy)
                    Status = (!append && Results.Count > 0 ? "正在更新搜索（列表为上次结果） · " : string.Empty)
                        + (p.Message ?? $"正在扫描 · 已发现 {p.Examined:N0} 项");
            }));
            var batchFiles = batch().ToArray();
            var page = await search.SearchAsync(request, batchFiles, progress, token);
            if (version != generation || token.IsCancellationRequested) return;
            if (append)
            {
                var paths = Results.Select(i => i.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in page.Items)
                    if (paths.Add(file.FullPath)) Results.Add(new(file, ColumnWidths));
            }
            else
            {
                // Publish a complete page once: typing and waiting never empty the visible list.
                Results = new(page.Items.DistinctBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase).Select(file => new SearchResultItem(file, ColumnWidths)));
                fetched = 0; SelectedItem = null; SelectionCount = 0;
            }
            fetched += page.Items.Count;
            Total = page.Total;
            completedRequest = request with { Offset = 0 };
            completedBatch = request.Scope == SearchScope.Batch ? batchFiles : null;
            Status = $"{page.Source} · {stopwatch.ElapsedMilliseconds:N0} ms";
            if (page.Skipped > 0) Status += $" · {page.Skipped:N0} 项无法读取";
            if (page.Items.Count == 0 && fetched < Total) { fetched = (int)Math.Min(Total, int.MaxValue); Feedback = "索引在查询期间发生变化，请刷新获取最新结果。"; }
        }
        catch (OperationCanceledException) { if (version == generation) { IsCancelled = true; Status = "已取消搜索 · 可修改条件或刷新"; } }
        catch (Exception e)
        {
            if (version != generation) return;
            Message = e is SearchException ? e.Message : "无法完成搜索，请检查目录权限后重试。";
            HasPageError = append;
            HasError = !append;
            Status = append ? $"后续结果加载失败 · 已载入 {Results.Count:N0} / {Total:N0} 项 · 可重试加载"
                : Results.Count > 0 ? "搜索未完成 · 列表保留上次结果" : "搜索未完成";
        }
        finally
        {
            if (version == generation)
            {
                pending = null; IsBusy = false; IsLoadingMore = false;
                NotifyResultActions(); OnPropertyChanged(nameof(ShowEmpty));
            }
        }
    }

    private void NotifyResultActions()
    {
        OnPropertyChanged(nameof(CanUseResults));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(ResultCountLabel));
    }
}
