using System.Collections.Concurrent;
using FileAssistant.App.Features.Search;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class SearchStateTests
{
    [Fact] public async Task Late_old_result_cannot_replace_new_query()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { QueryText = "old" };
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey("old"));
        vm.QueryText = "new";
        await WaitUntil(() => service.Pending.ContainsKey("new"));
        service.Pending["new"].SetResult(Page("new.txt"));
        await WaitUntil(() => !vm.IsBusy);
        service.Pending["old"].SetResult(Page("old.txt"));
        await Task.Delay(30);
        Assert.Equal("new.txt", Assert.Single(vm.Results).Name);
    }
    [Fact] public async Task Cancel_does_not_report_no_matches()
    {
        var vm = new SearchViewModel(new CancellableSearch(), () => [], _ => 0) { QueryText = "搜索中" };
        vm.Activate();
        vm.CancelCommand.Execute(null);
        await WaitUntil(() => !vm.IsBusy);
        Assert.True(vm.IsCancelled);
        Assert.Equal("搜索已取消", vm.EmptyTitle);
        Assert.False(vm.HasError);
    }
    [Fact] public async Task Ime_composition_does_not_launch_intermediate_queries()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { QueryText = "first" };
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey("first"));
        service.Pending["first"].SetResult(Page("first.txt"));
        await WaitUntil(() => !vm.IsBusy);
        vm.SetInputComposition(true);
        vm.QueryText = "chan";
        await Task.Delay(200);
        Assert.False(service.Pending.ContainsKey("chan"));
        vm.QueryText = "产品";
        vm.SetInputComposition(false);
        await WaitUntil(() => service.Pending.ContainsKey("产品"));
        service.Pending["产品"].SetResult(Page("产品.txt"));
        await WaitUntil(() => !vm.IsBusy);
        Assert.Equal("产品.txt", Assert.Single(vm.Results).Name);
    }
    [Fact] public void Batch_is_deduplicated_and_contains_real_entries_only()
    {
        var workspace = new WorkspaceViewModel();
        var first = new SearchFile(@"D:\文件\产品.png", false, 100, DateTimeOffset.Now);
        Assert.Equal(1, workspace.AddSearchFiles([first, first]));
        Assert.Equal(0, workspace.AddSearchFiles([first with { FullPath = @"d:\文件\产品.PNG" }]));
        Assert.Single(workspace.SearchEntries);
        Assert.False(workspace.HasSamples);
        workspace.SearchText = "no-match";
        Assert.Empty(workspace.VisibleFiles);
        Assert.Single(workspace.SearchEntries);
    }
    [Fact] public async Task Untouched_search_loads_all_types_and_supports_sorting_and_refresh()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0);
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey(""));
        Assert.Equal(SearchKind.All, service.LastRequest!.Kind);
        Assert.Equal(SearchScope.Everything, service.LastRequest.Scope);
        var initial = service.Pending[""];
        initial.SetResult(Page("all.txt") with { Total = 201 });
        await WaitUntil(() => !vm.IsBusy);
        Assert.Single(vm.Results);
        Assert.True(vm.CanLoadMore);
        vm.SortIndex = 1;
        await WaitUntil(() => service.Pending[""] != initial);
        Assert.True(service.LastRequest.Descending);
        service.Pending[""].SetResult(Page("sorted.txt"));
        await WaitUntil(() => !vm.IsBusy);
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(0, service.LastRequest.Offset);
        service.Pending[""].SetResult(Page("refreshed.txt"));
        await refresh;
        Assert.Equal("refreshed.txt", Assert.Single(vm.Results).Name);
        Assert.False(vm.ShowEmpty);
        Assert.False(vm.CanLoadMore);
    }
    [Fact] public async Task Clearing_query_restores_all_files_and_discards_a_late_filtered_result()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { QueryText = "old" };
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey("old"));
        vm.QueryText = "  ";
        await WaitUntil(() => service.Pending.ContainsKey("  "));
        service.Pending["  "].SetResult(Page("all.txt"));
        await WaitUntil(() => !vm.IsBusy);
        service.Pending["old"].SetResult(Page("old.txt"));
        await Task.Delay(30);
        Assert.Equal("all.txt", Assert.Single(vm.Results).Name);
        Assert.False(vm.IsBusy);
        Assert.False(vm.ShowEmpty);
        Assert.Equal(1, vm.Total);
    }
    [Fact] public async Task Resetting_a_type_filter_without_a_keyword_restores_all_files()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { KindIndex = 1 };
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey(""));
        var filtered = service.Pending[""];
        filtered.SetResult(Page("图片.png"));
        await WaitUntil(() => !vm.IsBusy);
        Assert.Single(vm.Results);
        vm.ResetFiltersCommand.Execute(null);
        await WaitUntil(() => service.Pending[""] != filtered);
        Assert.Equal(SearchKind.All, service.LastRequest!.Kind);
        service.Pending[""].SetResult(Page("all.txt"));
        await WaitUntil(() => !vm.IsBusy);
        Assert.Equal("all.txt", Assert.Single(vm.Results).Name);
        Assert.False(vm.ShowEmpty);
        Assert.True(vm.HasSearchSource);
    }
    [Fact] public void Shared_column_widths_keep_loaded_and_new_rows_aligned_and_preserve_user_resize()
    {
        var widths = new SearchColumnWidths();
        widths.FitViewport(1200);
        var first = new SearchResultItem(new(@"D:\first.txt", false, 1, null), widths);
        widths.Resize("Name", 150);
        var next = new SearchResultItem(new(@"D:\next.txt", false, 1, null), widths);
        Assert.Equal(410, first.Columns.Name);
        Assert.Same(first.Columns, next.Columns);
        var location = widths.Location;
        widths.FitViewport(800);
        Assert.Equal(location, widths.Location);
        widths.Resize("Size", -10000);
        Assert.Equal(64, widths.Size);
    }
    [Fact] public void File_actions_use_the_menu_target_and_copy_every_selected_path()
    {
        var actions = new RecordingInteraction();
        var vm = new SearchViewModel(new ControlledSearch(), () => [], _ => 0, actions);
        var first = new SearchResultItem(new(@"D:\目录\第一个.txt", false, 1, null));
        var target = new SearchResultItem(new(@"D:\目录\右键 文件.txt", false, 1, null));
        vm.SelectedItem = first;
        vm.OpenFile(target);
        Assert.Equal(target.File, actions.Opened);
        vm.OpenLocation(target);
        Assert.Equal(target.File, actions.Revealed);
        vm.CopySelection([first, target], false);
        Assert.Equal(first.FullPath + Environment.NewLine + target.FullPath, actions.Text);
        vm.CopySelection([first, target], true);
        Assert.Equal(first.Name + Environment.NewLine + target.Name, actions.Text);
        actions.Unavailable = true;
        vm.OpenFile(target);
        Assert.Contains("已移动", vm.Feedback);
    }
    [Fact] public async Task Executable_filter_reaches_the_search_service_and_reset_restores_button_labels()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { KindIndex = (int)SearchKind.Executables, SizeIndex = 3, DateIndex = 2 };
        Assert.Equal("可执行文件", vm.KindLabel);
        Assert.Equal("至少 10 MB", vm.SizeLabel);
        Assert.Equal("最近 7 天", vm.DateLabel);
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey(""));
        Assert.Equal(SearchKind.Executables, service.LastRequest!.Kind);
        service.Pending[""].SetResult(Page("app.exe"));
        await WaitUntil(() => !vm.IsBusy);
        vm.ResetFiltersCommand.Execute(null);
        Assert.Equal("所有类型", vm.KindLabel);
        Assert.Equal("不限大小", vm.SizeLabel);
        Assert.Equal("不限修改日期", vm.DateLabel);
        Assert.Empty(vm.Results);
    }
    [Fact] public async Task Choosing_a_folder_searches_without_text_and_clearing_text_restores_its_contents()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { ScopeIndex = (int)SearchScope.Folder };
        vm.Activate();
        Assert.Empty(service.Pending);
        Assert.Equal("指定目录", vm.ScopeLabel);
        vm.Folder = @"D:\选定文件夹";
        await WaitUntil(() => service.Pending.ContainsKey(""));
        Assert.Equal(SearchScope.Folder, service.LastRequest!.Scope);
        Assert.Equal(vm.Folder, service.LastRequest.Folder);
        service.Pending[""].SetResult(Page("目录中的文件.txt"));
        await WaitUntil(() => !vm.IsBusy);
        Assert.Single(vm.Results);
        vm.QueryText = "筛选";
        await WaitUntil(() => service.Pending.ContainsKey("筛选"));
        service.Pending["筛选"].SetResult(Page("筛选.txt"));
        await WaitUntil(() => !vm.IsBusy);
        vm.QueryText = string.Empty;
        await WaitUntil(() => service.LastRequest.Text == string.Empty && !service.Pending[""].Task.IsCompleted);
        service.Pending[""].SetResult(Page("目录中的文件.txt"));
        await WaitUntil(() => !vm.IsBusy);
        Assert.Equal("目录中的文件.txt", Assert.Single(vm.Results).Name);
    }
    [Fact] public async Task Leaving_folder_scope_loads_the_global_index_and_ignores_a_late_directory_scan()
    {
        var service = new ControlledSearch();
        var vm = new SearchViewModel(service, () => [], _ => 0) { ScopeIndex = (int)SearchScope.Folder, Folder = @"D:\样本" };
        vm.Activate();
        await WaitUntil(() => service.Pending.ContainsKey(""));
        var scan = service.Pending[""];
        vm.ScopeIndex = (int)SearchScope.Everything;
        Assert.Equal("全部已索引位置", vm.ScopeLabel);
        await WaitUntil(() => service.Pending[""] != scan);
        Assert.Equal(SearchScope.Everything, service.LastRequest!.Scope);
        service.Pending[""].SetResult(Page("indexed.txt"));
        await WaitUntil(() => !vm.IsBusy);
        scan.SetResult(Page("old-folder.txt"));
        await Task.Delay(30);
        Assert.Equal("indexed.txt", Assert.Single(vm.Results).Name);
        Assert.True(vm.HasSearchSource);
        Assert.False(vm.ShowEmpty);
    }
    private static SearchPage Page(string name) => new([new(@"D:\test\" + name, false, 1, DateTimeOffset.Now)], 1, "test");
    private static async Task WaitUntil(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }
    private sealed class ControlledSearch : IFileSearch
    {
        public SearchRequest? LastRequest { get; private set; }
        public ConcurrentDictionary<string, TaskCompletionSource<SearchPage>> Pending { get; } = new();
        public Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var completion = new TaskCompletionSource<SearchPage>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending[request.Text] = completion;
            return completion.Task; // Deliberately ignores cancellation to test stale completion protection.
        }
        public void InvalidateFolder() { }
    }
    private sealed class CancellableSearch : IFileSearch
    {
        public async Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
        { await Task.Delay(Timeout.Infinite, cancellationToken); return Page("unused"); }
        public void InvalidateFolder() { }
    }
    private sealed class RecordingInteraction : IFileInteraction
    {
        public SearchFile? Opened, Revealed;
        public string? Text;
        public bool Unavailable;
        public void Open(SearchFile file) { if (Unavailable) throw new FileInteractionException("文件已移动，请刷新。"); Opened = file; }
        public void Reveal(SearchFile file) => Revealed = file;
        public void CopyText(string text) => Text = text;
    }
}
