using FileAssistant.App.Features.Search;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class SearchExperienceTests
{
    [Fact]
    public async Task Rapid_text_and_filter_changes_issue_only_the_final_debounced_query()
    {
        var search = new RecordingSearch();
        var vm = new SearchViewModel(search, () => [], TestImports.None);
        vm.Activate();
        vm.QueryText = "p"; vm.QueryText = "pr"; vm.QueryText = "产品";
        vm.KindIndex = (int)SearchKind.Images; vm.SortBy(SearchSort.Size); vm.SortBy(SearchSort.Size);
        Assert.Equal(1, search.Calls);
        await Settled(vm);
        Assert.Equal(2, search.Calls);
        Assert.Equal("产品", search.Last!.Text);
        Assert.Equal(SearchKind.Images, search.Last.Kind);
        Assert.Equal(SearchSort.Size, search.Last.Sort);
        Assert.True(search.Last.Descending);
        Assert.Empty(vm.NameSortGlyph);
        Assert.NotEmpty(vm.SizeSortGlyph);
        Assert.Contains("当前降序", vm.SizeSortDescription);
        vm.Deactivate();
    }

    [Fact]
    public async Task Count_strip_survives_updates_and_marks_retained_results()
    {
        var vm = new SearchViewModel(new RecordingSearch(), () => [], TestImports.None);
        vm.Activate(); vm.SelectionCount = 1;
        Assert.Equal("匹配 201 项 · 已加载 1 项 · 已选 1 项", vm.ResultCountLabel);
        vm.QueryText = "new";
        Assert.StartsWith("上次结果 · 匹配 201 项 · 已加载 1 项 · 已选 1 项", vm.ResultCountLabel);
        await Settled(vm);
        Assert.Equal("匹配 201 项 · 已加载 1 项 · 已选 0 项", vm.ResultCountLabel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Count_binding_updates_when_folder_source_is_missing_or_refresh_is_cancelled(bool missingFolder)
    {
        var vm = new SearchViewModel(new RecordingSearch(), () => [], TestImports.None);
        vm.Activate();
        var displayed = vm.ResultCountLabel;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.ResultCountLabel)) displayed = vm.ResultCountLabel; };
        if (missingFolder)
        {
            vm.ScopeIndex = (int)SearchScope.Folder;
            Assert.Empty(vm.Results);
            Assert.Equal("匹配 — 项 · 已加载 0 项 · 已选 0 项", displayed);
        }
        else
        {
            vm.QueryText = "new";
            vm.QueryText = string.Empty;
            vm.CancelCommand.Execute(null);
            Assert.False(vm.IsBusy);
            Assert.Equal("匹配 201 项 · 已加载 1 项 · 已选 0 项", displayed);
        }
    }

    [Theory]
    [InlineData("-1", "10")]
    [InlineData("20", "10")]
    [InlineData("abc", "10")]
    [InlineData("", "")]
    [InlineData("999999999999999999999999999", "")]
    public void Invalid_custom_size_does_not_query_or_erase_previous_results(string minimum, string maximum)
    {
        var search = new RecordingSearch();
        var vm = new SearchViewModel(search, () => [], TestImports.None);
        vm.Activate();
        var results = vm.Results;
        vm.SizeIndex = 6; vm.CustomMinimum = minimum; vm.CustomMaximum = maximum;
        Assert.True(vm.HasFilterError);
        Assert.False(vm.IsBusy);
        Assert.False(vm.CanUseResults);
        Assert.Same(results, vm.Results);
        Assert.Equal(1, search.Calls);
    }

    [Fact]
    public async Task Custom_size_converts_units_and_rounds_to_integer_byte_bounds()
    {
        var search = new RecordingSearch();
        var vm = new SearchViewModel(search, () => [], TestImports.None)
        { SizeIndex = 6, SizeUnitIndex = 1, CustomMinimum = "1.1", CustomMaximum = "1.2" };
        vm.Activate();
        Assert.Equal(1127, search.Last!.MinimumSize);
        Assert.Equal(1228, search.Last.MaximumSize);
        vm.CustomMinimum = string.Empty;
        await Settled(vm);
        Assert.Null(search.Last.MinimumSize);
        Assert.Equal(1228, search.Last.MaximumSize);
        vm.CustomMaximum = "0";
        await Settled(vm);
        Assert.Equal(0, search.Last.MaximumSize);
        Assert.False(vm.HasFilterError);
    }

    [Fact]
    public async Task Custom_dates_use_local_midnights_and_include_the_end_day()
    {
        var search = new RecordingSearch();
        var day = new DateTimeOffset(2026, 10, 30, 14, 0, 0, TimeSpan.FromHours(8));
        var vm = new SearchViewModel(search, () => [], TestImports.None)
        { DateIndex = 4, CustomDateFrom = day, CustomDateThrough = day };
        vm.Activate();
        Assert.Equal(new DateTime(2026, 10, 30), search.Last!.ModifiedFrom!.Value.LocalDateTime);
        Assert.Equal(new DateTime(2026, 10, 31), search.Last.ModifiedBefore!.Value.LocalDateTime);
        vm.CustomDateFrom = day.AddDays(1);
        Assert.True(vm.HasFilterError);
        Assert.Equal(1, search.Calls);
        vm.CustomDateFrom = null;
        await Settled(vm);
        Assert.Null(search.Last.ModifiedFrom);
        Assert.NotNull(search.Last.ModifiedBefore);
    }

    [Fact]
    public async Task Repeated_load_more_requests_share_the_single_in_flight_page()
    {
        var search = new RecordingSearch { HoldNextPage = true };
        var vm = new SearchViewModel(search, () => [], TestImports.None);
        vm.Activate();
        var first = vm.LoadMoreCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Equal(2, search.Calls);
        Assert.Equal(1, search.Last!.Offset);
        search.NextPage.SetResult(new([new(@"D:\second.txt", false, 2, null)], 2, "test"));
        await first;
        Assert.Equal("匹配 2 项 · 已加载 2 项 · 已选 0 项", vm.ResultCountLabel);
    }

    [Fact]
    public void Progress_is_throttled_before_dispatch_but_stage_changes_are_immediate()
    {
        var clock = new ManualClock();
        var progress = new RecordedProgress();
        var throttle = new ThrottledSearchProgress(progress, clock);
        for (var i = 0; i < 1000; i++) throttle.Report(new(i, "test"));
        Assert.Single(progress.Values);
        clock.Milliseconds = 99; throttle.Report(new(1000, "test"));
        Assert.Single(progress.Values);
        clock.Milliseconds = 100; throttle.Report(new(1001, "test"));
        Assert.Equal(2, progress.Values.Count);
        throttle.Report(new(1002, "test", "正在准备索引"));
        Assert.Equal(3, progress.Values.Count);
    }

    internal static async Task Settled(SearchViewModel vm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (vm.IsBusy) await Task.Delay(10, timeout.Token);
    }

    internal sealed class RecordingSearch : IFileSearch
    {
        public int Calls;
        public SearchRequest? Last;
        public bool HoldNextPage;
        public TaskCompletionSource<SearchPage> NextPage { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
        {
            Calls++; Last = request;
            return request.Offset > 0 && HoldNextPage ? NextPage.Task
                : Task.FromResult(new SearchPage([new(@"D:\first.txt", false, 1, null)], 201, "test"));
        }
        public void InvalidateFolder() { }
    }
    private sealed class ManualClock : TimeProvider
    {
        public long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
    private sealed class RecordedProgress : IProgress<SearchProgress>
    {
        public List<SearchProgress> Values { get; } = [];
        public void Report(SearchProgress value) => Values.Add(value);
    }
}
