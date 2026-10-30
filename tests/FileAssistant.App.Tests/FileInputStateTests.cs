using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class FileInputStateTests
{
    [Fact]
    public async Task Independent_inputs_append_and_dedupe_without_automatically_synchronizing()
    {
        var reader = new ReadyReader();
        var staging = new WorkspaceViewModel(reader);
        var feature = new WorkspaceViewModel(reader, isStaging: false);
        await staging.ImportPathsAsync([@"D:\a.txt", @"D:\b.txt"]);
        await feature.ImportPathsAsync([@"D:\c.txt", @"D:\a.txt"]);
        Assert.Equal(2, staging.SearchEntries.Count);
        var report = await feature.ImportFilesAsync(staging.SearchEntries);
        Assert.Equal(1, report.Added);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal([@"D:\c.txt", @"D:\a.txt", @"D:\b.txt"], feature.SearchEntries.Select(file => file.FullPath));
        staging.ClearCommand.Execute(null);
        Assert.Empty(staging.SearchEntries);
        Assert.Equal(3, feature.SearchEntries.Count);
        await staging.ImportFilesAsync([feature.SearchEntries[0]]);
        feature.RemoveFromList([feature.VisibleFiles[0]]);
        feature.ClearCommand.Execute(null);
        Assert.Equal(@"D:\c.txt", Assert.Single(staging.SearchEntries).FullPath);
    }

    [Fact]
    public async Task Transfer_revalidates_references_and_reports_missing_unsupported_and_duplicate_items()
    {
        var reader = new ReadyReader();
        var staging = new WorkspaceViewModel(reader);
        await staging.ImportPathsAsync([@"D:\gone.txt", @"D:\good.txt", @"D:\unsupported.txt"]);
        var feature = new WorkspaceViewModel(reader, false);
        await feature.ImportPathsAsync([@"D:\good.txt"]);
        reader.Missing = @"D:\gone.txt";
        reader.Unsupported = @"D:\unsupported.txt";
        var report = await feature.ImportFilesAsync(staging.SearchEntries);
        Assert.Equal(0, report.Added);
        Assert.Equal(1, report.Unavailable);
        Assert.Equal(1, report.Unsupported);
        Assert.Equal(1, report.Duplicates);
        Assert.Single(feature.SearchEntries);
        Assert.Equal(3, staging.SearchEntries.Count);
        Assert.Equal(report, feature.ImportReport);
    }

    [Fact]
    public async Task Cancel_and_clear_ignore_late_reader_results()
    {
        var reader = new ControlledReader();
        var vm = new WorkspaceViewModel(reader);
        vm.AddSearchFiles([File(@"D:\before.txt")]);
        var adding = vm.ImportPathsAsync([@"D:\late.txt"]);
        Assert.True(vm.IsLoading);
        Assert.False(vm.CanReceiveFiles);
        vm.ClearCommand.Execute(null);
        Assert.True(reader.Token.IsCancellationRequested);
        reader.Completion.SetResult(Ready([@"D:\late.txt"]));
        var report = await adding;
        Assert.Empty(vm.SearchEntries);
        Assert.False(vm.IsLoading);
        Assert.Empty(vm.InputFeedback);
        Assert.Equal(0, report.Added);
    }

    [Fact]
    public async Task Repeated_import_is_rejected_while_reading_and_cancel_keeps_previous_list()
    {
        var reader = new ControlledReader();
        var vm = new WorkspaceViewModel(reader);
        vm.AddSearchFiles([File(@"D:\before.txt")]);
        var adding = vm.ImportPathsAsync([@"D:\late.txt"]);
        var repeated = await vm.ImportPathsAsync([@"D:\duplicate-click.txt"]);
        Assert.Contains("正在添加", repeated.Summary);
        Assert.Equal(1, reader.Calls);
        vm.CancelLoadingCommand.Execute(null);
        reader.Completion.SetResult(Ready([@"D:\late.txt"]));
        await adding;
        Assert.Equal(@"D:\before.txt", Assert.Single(vm.SearchEntries).FullPath);
        Assert.True(vm.CanReceiveFiles);
    }

    [Fact]
    public async Task Cancelled_reader_cannot_overwrite_a_newer_import_or_its_feedback()
    {
        var reader = new ControlledReader();
        var vm = new WorkspaceViewModel(reader);
        var first = vm.ImportPathsAsync([@"D:\old.txt"]);
        var oldCompletion = reader.Completion;
        vm.CancelLoadingCommand.Execute(null);
        reader.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = vm.ImportPathsAsync([@"D:\current.txt"]);
        reader.Completion.SetResult(Ready([@"D:\current.txt"]));
        var currentReport = await current;
        oldCompletion.SetResult(Ready([@"D:\old.txt"]));
        await first;
        Assert.Equal(@"D:\current.txt", Assert.Single(vm.SearchEntries).FullPath);
        Assert.Same(currentReport, vm.ImportReport);
        Assert.Equal(currentReport.Summary, vm.InputFeedback);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Failed_reader_preserves_existing_rows_and_releases_busy_state()
    {
        var reader = new ControlledReader();
        var vm = new WorkspaceViewModel(reader);
        vm.AddSearchFiles([File(@"D:\before.txt")]);
        var adding = vm.ImportPathsAsync([@"D:\new.txt"]);
        reader.Completion.SetException(new IOException("provider failure"));
        var report = await adding;
        Assert.Contains("原列表已保留", report.Summary);
        Assert.Single(vm.SearchEntries);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Real_import_replaces_samples_and_samples_cannot_replace_real_files()
    {
        var vm = new WorkspaceViewModel(new ReadyReader());
        await vm.LoadSamplesCommand.ExecuteAsync(null);
        Assert.True(vm.HasSamples);
        Assert.Empty(vm.SearchEntries);
        Assert.False(vm.CanExportAll);
        await vm.ImportPathsAsync([@"D:\real.txt"]);
        Assert.False(vm.HasSamples);
        Assert.Single(vm.VisibleFiles);
        await vm.LoadSamplesCommand.ExecuteAsync(null);
        Assert.Equal(@"D:\real.txt", Assert.Single(vm.SearchEntries).FullPath);
    }

    [Fact]
    public async Task Empty_selection_does_not_enable_export_and_filters_do_not_change_all_scope()
    {
        var vm = new WorkspaceViewModel(new ReadyReader());
        await vm.ImportPathsAsync([@"D:\a.txt", @"D:\b.txt"]);
        Assert.False(vm.CanExportSelection);
        Assert.True(vm.CanExportAll);
        vm.SearchText = "a.txt";
        Assert.Single(vm.VisibleFiles);
        Assert.Equal(2, vm.SearchEntries.Count);
        Assert.Contains("全部 2 项", vm.SendAllLabel);
        vm.SelectionCount = 1;
        Assert.True(vm.CanExportSelection);
        Assert.Contains("选中 1 项", vm.SendSelectedLabel);
    }

    private static SearchFile File(string path) => new(path, false, 1, null);
    private static FileImportReport Ready(IEnumerable<string> paths) => new(paths.Select(path => new FileImportItem(path, FileImportStatus.Ready, "文件可用。", File(path))).ToArray());
    private sealed class ReadyReader : IFileInputReader
    {
        public string? Missing { get; set; }
        public string? Unsupported { get; set; }
        public Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) => Task.FromResult(new FileImportReport(
            paths.Select(path => path == Missing ? new FileImportItem(path, FileImportStatus.Unavailable, "项目已删除。")
                : path == Unsupported ? new FileImportItem(path, FileImportStatus.Unsupported, "此功能不支持。")
                : new FileImportItem(path, FileImportStatus.Ready, "文件可用。", File(path))).ToArray()));
    }
    private sealed class ControlledReader : IFileInputReader
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource<FileImportReport> Completion { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        { Calls++; Token = cancellationToken; return Completion.Task; }
    }
}
