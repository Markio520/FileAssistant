using FileAssistant.App.Features.Convert;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class ConversionWorkflowTests
{
    private readonly FakeProcessing service = new();
    private readonly WorkspaceViewModel input = new(null, WorkspaceInputPurpose.Conversion);
    private readonly OutputsViewModel outputs = new(new Interaction());
    private readonly ProcessingJobsViewModel jobs = new();

    [Fact]
    public async Task Plans_become_invalid_when_inputs_or_conversion_options_change()
    {
        var viewModel = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\a.png", false, 1, null)]);
        await viewModel.BuildPlanCommand.ExecuteAsync(null);
        Assert.True(viewModel.CanRun);
        input.SearchText = "different display filter";
        Assert.True(viewModel.CanRun); // Display filtering must not change the input set.
        viewModel.FormatIndex = (int)ConversionTarget.WebP;
        Assert.False(viewModel.CanRun);
        await viewModel.BuildPlanCommand.ExecuteAsync(null);
        input.AddSearchFiles([new(@"C:\test\b.png", false, 1, null)]);
        Assert.Null(viewModel.Plan);
        Assert.Empty(jobs.Items);
        Assert.False(outputs.HasFiles);
    }

    [Fact]
    public async Task A_plan_that_returns_after_inputs_changed_cannot_be_executed()
    {
        var pending = new TaskCompletionSource<ConversionPlan>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Pending = pending.Task;
        var viewModel = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\a.png", false, 1, null)]);
        var task = viewModel.BuildPlanCommand.ExecuteAsync(null);
        input.AddSearchFiles([new(@"C:\test\b.png", false, 1, null)]);
        pending.SetResult(FakeProcessing.Plan(service.Request!));
        await task;
        Assert.False(viewModel.CanRun);
        Assert.Null(viewModel.Plan);
        Assert.Contains("已变化", viewModel.Feedback);
    }

    [Fact]
    public async Task Only_actual_outputs_are_recorded_and_late_progress_cannot_reopen_completed_jobs()
    {
        var viewModel = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\a.png", false, 1, null)]);
        await viewModel.BuildPlanCommand.ExecuteAsync(null);
        await viewModel.RunCommand.ExecuteAsync(null);
        var job = Assert.Single(jobs.Items);
        Assert.False(job.IsRunning);
        Assert.Single(outputs.VisibleFiles);
        Assert.Single(input.SearchEntries);
        Assert.False(viewModel.CanRun);
        var status = job.Status;
        job.Update(new(0, 1, "late update"));
        Assert.Equal(status, job.Status);
        var shownTask = 0;
        viewModel.OutputsRequested += (_, id) => shownTask = id;
        viewModel.ShowOutputsCommand.Execute(null);
        Assert.Equal(job.Id, shownTask);
    }

    [Fact]
    public void Output_filters_and_clear_records_preserve_the_produced_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "real output");
            var file = new ProducedFile(path, 11, DateTimeOffset.UtcNow, [@"C:\source.png"]);
            outputs.Add(1, "task one", [file]);
            outputs.Add(2, "task two", [file with { FullPath = path + ".other" }]);
            outputs.TaskId = 1;
            var selected = Assert.Single(outputs.VisibleFiles);
            outputs.SetSelection([selected]);
            outputs.SearchText = "not present";
            Assert.Empty(outputs.VisibleFiles);
            Assert.False(outputs.HasSelection);
            outputs.ClearRecordsCommand.Execute(null);
            Assert.False(outputs.HasFiles);
            Assert.Single(outputs.Tasks);
            Assert.Equal("real output", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class FakeProcessing : IFileProcessingService
    {
        public IReadOnlyList<ProcessingEngineInfo> Engines => [];
        public Task<ConversionPlan>? Pending { get; set; }
        public ConversionRequest? Request { get; private set; }
        public Task<PreviewResult> PreviewAsync(string path, int maximumEdge, CancellationToken token) => throw new NotSupportedException();
        public Task<ConversionPlan> CreatePlanAsync(ConversionRequest request, CancellationToken token)
        { Request = request; return Pending ?? Task.FromResult(Plan(request)); }
        public static ConversionPlan Plan(ConversionRequest request) => new(request,
            [new([new(request.Paths[0], 1, 1)], Path.Combine(request.OutputDirectory, "converted.png"))]);
        public Task<ConversionReport> ConvertAsync(ConversionPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token)
            => Task.FromResult(new ConversionReport([new(plan.Items[0], new(plan.Items[0].OutputPath!, 42, DateTimeOffset.UtcNow, plan.Request.Paths), "已完成")], false));
    }
    private sealed class Interaction : IFileInteraction
    { public void Open(SearchFile file) { } public void Reveal(SearchFile file) { } public void CopyText(string text) { } }
}
