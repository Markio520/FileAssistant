using System.Collections.Concurrent;
using FileAssistant.App.Features.Search;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class SearchStabilityTests
{
    [Fact]
    public async Task Adding_search_selection_uses_a_snapshot_and_rejects_duplicate_in_flight_clicks()
    {
        var service = new ControlledSearch();
        var completion = new TaskCompletionSource<FileAssistant.Core.Features.FileInput.FileImportReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<SearchFile>? received = null;
        var calls = 0;
        var vm = new SearchViewModel(service, () => [], files => { calls++; received = files; return completion.Task; });
        vm.Activate();
        (await service.Next()).Completion.SetResult(Page("selected.txt"));
        await Settled(vm);
        SelectFirst(vm);
        var selected = vm.Results.ToArray();
        var adding = vm.AddSelectionAsync(selected);
        Assert.True(vm.IsAddingToStaging);
        Assert.False(vm.CanAdd);
        await vm.AddSelectionAsync(selected);
        vm.QueryText = "changed";
        (await service.Next()).Completion.SetResult(Page("new.txt"));
        await Settled(vm);
        Assert.Equal("selected.txt", Assert.Single(received!).Name);
        completion.SetResult(await TestImports.Added(received!));
        await adding;
        Assert.Equal(1, calls);
        Assert.False(vm.IsAddingToStaging);
        Assert.Contains("已加入 1 项", vm.Feedback);
        Assert.False(vm.CanAdd); // New results have no selection.
        Assert.Equal("new.txt", Assert.Single(vm.Results).Name);
    }

    [Fact]
    public async Task Failed_addition_releases_the_gate_for_retry()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => throw new IOException("read failed"));
        vm.Activate();
        (await service.Next()).Completion.SetResult(Page("selected.txt"));
        await Settled(vm);
        SelectFirst(vm);
        await vm.AddSelectionAsync(vm.Results.ToArray());
        Assert.False(vm.IsAddingToStaging);
        Assert.True(vm.CanAdd);
        Assert.Contains("无法加入暂存区", vm.Feedback);
        Assert.Single(vm.Results);
    }

    [Fact]
    public async Task Typing_keeps_the_complete_previous_page_until_new_results_arrive()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        var previous = vm.Results;
        SelectFirst(vm);
        var publishedCounts = new List<int>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Results)) publishedCounts.Add(vm.Results.Count); };

        vm.QueryText = "new";
        Assert.Same(previous, vm.Results); // Includes the debounce interval.
        Assert.Equal(2, vm.Results.Count);
        Assert.Equal(5, vm.Total);
        Assert.Equal(1, vm.SelectionCount);
        Assert.False(vm.CanUseResults);
        Assert.Contains("上次结果", vm.Status);
        var request = await service.Next();
        Assert.Same(previous, vm.Results);
        request.Completion.SetResult(Page("new.txt"));
        await Settled(vm);

        Assert.Equal("new.txt", Assert.Single(vm.Results).Name);
        Assert.Equal([1], publishedCounts);
        Assert.Equal(0, vm.SelectionCount);
        Assert.Null(vm.SelectedItem);
        Assert.True(vm.CanUseResults);
    }

    [Fact]
    public async Task Returning_to_a_completed_search_preserves_collection_rows_selection_and_page_count()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        SelectFirst(vm);
        var previous = vm.Results;
        var selected = vm.SelectedItem;
        var status = vm.Status;
        vm.Deactivate();
        vm.Activate();
        await Task.Delay(200);

        Assert.Equal(1, service.CallCount);
        Assert.Same(previous, vm.Results);
        Assert.Same(selected, vm.SelectedItem);
        Assert.Equal(1, vm.SelectionCount);
        Assert.Equal(5, vm.Total);
        Assert.Equal(status, vm.Status);
        Assert.True(vm.CanAdd);
        Assert.True(vm.CanLoadMore);
    }

    [Fact]
    public async Task Failed_next_page_keeps_existing_rows_usable_and_retries_the_same_offset()
    {
        var service = new ControlledSearch();
        var actions = new RecordingInteraction();
        var added = 0;
        var vm = await LoadInitial(service, files => added += files.Count, actions);
        SelectFirst(vm);
        var previous = vm.Results;
        var first = vm.Results[0];
        var load = vm.LoadMoreCommand.ExecuteAsync(null);
        var failed = await service.Next();
        Assert.Equal(2, failed.Request.Offset);
        Assert.True(vm.CanAdd);
        Assert.False(vm.CanLoadMore);
        failed.Completion.SetException(new SearchException("索引暂时无响应"));
        await load;

        Assert.Same(previous, vm.Results);
        Assert.Same(first, vm.SelectedItem);
        Assert.Equal(5, vm.Total);
        Assert.False(vm.HasError);
        Assert.True(vm.HasPageError);
        Assert.False(vm.ShowSearchError);
        Assert.True(vm.ShowInlineError);
        Assert.Equal("重试加载", vm.LoadMoreLabel);
        Assert.True(vm.CanLoadMore);
        await vm.AddSelectionAsync([first]);
        vm.CopySelection([first], false);
        vm.OpenFile(first);
        Assert.Equal(1, added);
        Assert.Equal(first.FullPath, actions.Text);
        Assert.Equal(first.File, actions.Opened);

        var retry = vm.RetryCommand.ExecuteAsync(null);
        var retried = await service.Next();
        Assert.Equal(failed.Request, retried.Request);
        retried.Completion.SetResult(Page("third.txt") with { Total = 5 });
        await retry;
        Assert.Same(previous, vm.Results);
        Assert.Equal(3, vm.Results.Count);
        Assert.Same(first, vm.SelectedItem);
        Assert.Equal(1, vm.SelectionCount);
        Assert.False(vm.HasPageError);
        Assert.False(vm.ShowInlineError);
        Assert.Equal("加载更多", vm.LoadMoreLabel);
        Assert.Equal(0, service.Invalidations);
    }

    [Fact]
    public async Task Failed_replacement_keeps_old_rows_but_never_treats_them_as_new_matches()
    {
        var service = new ControlledSearch();
        var added = 0;
        var vm = await LoadInitial(service, files => added += files.Count);
        SelectFirst(vm);
        var previous = vm.Results;
        vm.QueryText = "new";
        (await service.Next()).Completion.SetException(new SearchException("无效表达式"));
        await Settled(vm);
        Assert.Same(previous, vm.Results);
        Assert.True(vm.HasError);
        Assert.True(vm.ShowInlineError);
        Assert.False(vm.ShowSearchError);
        Assert.False(vm.CanUseResults);
        Assert.False(vm.CanAdd);
        Assert.False(vm.CanLoadMore);
        await vm.AddSelectionAsync([previous[0]]);
        Assert.Equal(0, added);

        var retry = vm.RetryCommand.ExecuteAsync(null);
        var request = await service.Next();
        Assert.Equal("new", request.Request.Text);
        Assert.Equal(0, request.Request.Offset);
        request.Completion.SetResult(new([], 0, "test"));
        await retry;
        Assert.Empty(vm.Results);
        Assert.True(vm.ShowEmpty);
        Assert.False(vm.HasError);
        Assert.False(vm.ShowInlineError);
    }

    [Fact]
    public async Task Initial_failure_still_shows_the_full_error_and_can_retry()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], TestImports.None);
        vm.Activate();
        (await service.Next()).Completion.SetException(new SearchException("索引未就绪"));
        await Settled(vm);
        Assert.True(vm.ShowSearchError);
        Assert.False(vm.ShowInlineError);
        Assert.False(vm.ShowEmpty);
        var retry = vm.RetryCommand.ExecuteAsync(null);
        (await service.Next()).Completion.SetResult(Page("ready.txt"));
        await retry;
        Assert.True(vm.CanUseResults);
        Assert.False(vm.ShowSearchError);
    }

    [Fact]
    public async Task Cancelled_replacement_cannot_publish_a_late_page_or_append_old_query_results()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        var previous = vm.Results;
        vm.QueryText = "new";
        var cancelled = await service.Next();
        vm.CancelCommand.Execute(null);
        Assert.False(vm.IsBusy);
        Assert.True(vm.IsCancelled);
        Assert.Same(previous, vm.Results);
        Assert.False(vm.CanLoadMore);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Equal(2, service.CallCount);
        cancelled.Completion.SetResult(Page("too-late.txt"));
        await Task.Delay(50);
        Assert.Same(previous, vm.Results);
        Assert.Contains("上次结果", vm.Status);
    }

    [Fact]
    public async Task Leaving_during_replacement_resumes_on_return_and_ignores_the_cancelled_response()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        var previous = vm.Results;
        vm.QueryText = "new";
        var interrupted = await service.Next();
        vm.Deactivate();
        Assert.True(interrupted.Token.IsCancellationRequested);
        vm.Activate();
        var resumed = await service.Next();
        Assert.Same(previous, vm.Results);
        Assert.Equal(interrupted.Request, resumed.Request);
        resumed.Completion.SetResult(Page("current.txt"));
        await Settled(vm);
        interrupted.Completion.SetException(new SearchException("迟到的失败"));
        await Task.Delay(50);
        Assert.Equal("current.txt", Assert.Single(vm.Results).Name);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Leaving_during_pagination_keeps_selection_and_allows_loading_the_same_page_after_return()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        SelectFirst(vm);
        var previous = vm.Results;
        var load = vm.LoadMoreCommand.ExecuteAsync(null);
        var interrupted = await service.Next();
        vm.Deactivate();
        vm.Activate();
        Assert.False(vm.IsBusy);
        Assert.True(vm.CanAdd);
        Assert.True(vm.CanLoadMore);
        Assert.Equal(2, service.CallCount);
        interrupted.Completion.SetResult(Page("late.txt"));
        await load;
        Assert.Same(previous, vm.Results);
        Assert.Equal(2, vm.Results.Count);
        Assert.Equal(1, vm.SelectionCount);
        var retry = vm.LoadMoreCommand.ExecuteAsync(null);
        var retried = await service.Next();
        Assert.Equal(2, retried.Request.Offset);
        retried.Completion.SetResult(Page("third.txt") with { Total = 3 });
        await retry;
        Assert.Equal(3, vm.Results.Count);
        Assert.False(vm.CanLoadMore);
    }

    [Fact]
    public async Task Changed_conditions_while_hidden_are_queried_when_the_page_returns()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        vm.Deactivate();
        vm.KindIndex = (int)SearchKind.Images;
        Assert.Equal(1, service.CallCount);
        vm.Activate();
        var request = await service.Next();
        Assert.Equal(SearchKind.Images, request.Request.Kind);
        request.Completion.SetResult(Page("image.png"));
        await Settled(vm);
        Assert.Equal("image.png", Assert.Single(vm.Results).Name);
    }

    [Fact]
    public async Task Batch_scope_rechecks_changed_batch_contents_on_return()
    {
        var service = new ControlledSearch();
        var files = new List<SearchFile> { File("first.txt") };
        var vm = new SearchViewModel(service, () => files, TestImports.None) { ScopeIndex = (int)SearchScope.Batch };
        vm.Activate();
        (await service.Next()).Completion.SetResult(new(files.ToArray(), 1, "batch"));
        await Settled(vm);
        vm.Deactivate();
        files.Clear();
        vm.Activate();
        var request = await service.Next();
        Assert.Empty(request.Batch);
        request.Completion.SetResult(new([], 0, "batch"));
        await Settled(vm);
        Assert.True(vm.ShowEmpty);
    }

    [Fact]
    public async Task Refresh_keeps_previous_rows_until_a_successful_replacement()
    {
        var service = new ControlledSearch();
        var vm = await LoadInitial(service);
        var previous = vm.Results;
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        var request = await service.Next();
        Assert.Equal(0, request.Request.Offset);
        Assert.Same(previous, vm.Results);
        Assert.Equal(1, service.Invalidations);
        request.Completion.SetResult(Page("refreshed.txt"));
        await refresh;
        Assert.Equal("refreshed.txt", Assert.Single(vm.Results).Name);
    }

    private static void SelectFirst(SearchViewModel vm) { vm.SelectedItem = vm.Results[0]; vm.SelectionCount = 1; }
    private static SearchFile File(string name) => new(@"D:\test\" + name, false, 1, null);
    private static SearchPage Page(string name) => new([File(name)], 1, "test");
    private static async Task<SearchViewModel> LoadInitial(ControlledSearch service,
        Func<IReadOnlyList<SearchFile>, int>? add = null, IFileInteraction? interaction = null)
    {
        var vm = new SearchViewModel(service, () => [], files => TestImports.Added(files.Take(add?.Invoke(files) ?? 0)), interaction);
        vm.Activate();
        (await service.Next()).Completion.SetResult(new([File("first.txt"), File("second.txt")], 5, "test"));
        await Settled(vm);
        return vm;
    }
    private static async Task Settled(SearchViewModel vm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (vm.IsBusy) await Task.Delay(10, timeout.Token);
    }
    private sealed record PendingSearch(SearchRequest Request, IReadOnlyList<SearchFile> Batch, CancellationToken Token)
    {
        public TaskCompletionSource<SearchPage> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class ControlledSearch : IFileSearch
    {
        private readonly ConcurrentQueue<PendingSearch> pending = new();
        public int CallCount { get; private set; }
        public int Invalidations { get; private set; }
        public Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
        {
            CallCount++;
            var query = new PendingSearch(request, batch, cancellationToken);
            pending.Enqueue(query);
            return query.Completion.Task; // Also exercise providers that finish after cancellation.
        }
        public async Task<PendingSearch> Next()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            while (true)
            {
                if (pending.TryDequeue(out var query)) return query;
                await Task.Delay(10, timeout.Token);
            }
        }
        public void InvalidateFolder() => Invalidations++;
    }
    private sealed class RecordingInteraction : IFileInteraction
    {
        public string? Text;
        public SearchFile? Opened;
        public void Open(SearchFile file) => Opened = file;
        public void Reveal(SearchFile file) { }
        public void CopyText(string text) => Text = text;
    }
}
