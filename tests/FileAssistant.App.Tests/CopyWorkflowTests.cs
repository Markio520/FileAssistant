using FileAssistant.App.Features.Organize;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Organize;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

public sealed class CopyWorkflowTests
{
    private readonly WorkspaceViewModel input = new(null, WorkspaceInputPurpose.Copy);
    private readonly OutputsViewModel outputs = new(new Interaction());
    private readonly ProcessingJobsViewModel jobs = new();
    private readonly FakeCopy service = new();
    private CopyViewModel Model() => new(service, input, outputs, jobs) { Destination = @"D:\out" };
    private void Add() => input.AddSearchFiles([new(@"C:\a.txt", false, 10, null), new(@"C:\b.txt", false, 20, null)]);

    [Fact]
    public async Task Filtering_preserves_hidden_selection_and_the_fixed_plan()
    {
        var vm = Model(); Add(); input.SetSelection([input.VisibleFiles[0]]);
        await vm.BuildPlanCommand.ExecuteAsync(null); var plan = vm.Plan;
        // Native selection removal events during a collection refresh must be ignored.
        input.VisibleFiles.CollectionChanged += (_, _) => input.SetVisibleSelection([]);
        input.SearchText = "b.txt";
        Assert.Equal(0, input.VisibleSelectedCount); Assert.Single(input.EffectiveEntries);
        Assert.Equal(@"C:\a.txt", input.EffectiveEntries[0].FullPath); Assert.Same(plan, vm.Plan);
        Assert.Contains("当前可见 0", vm.ScopeSummary);
        input.SetVisibleSelection([input.VisibleFiles[0]]);
        Assert.Equal(2, input.SelectionCount);
        vm.ClearSelectionCommand.Execute(null);
        Assert.Equal(0, input.SelectionCount); Assert.Equal(2, input.EffectiveEntries.Count);
    }
    [Fact]
    public async Task Cancelled_or_outdated_scan_cannot_become_an_executable_plan()
    {
        var vm = Model(); Add();
        var pending = new TaskCompletionSource<CopyPlan>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PendingPlan = pending.Task;
        var building = vm.BuildPlanCommand.ExecuteAsync(null);
        vm.Destination = @"D:\different";
        pending.SetResult(FakeCopy.Plan(service.Request!));
        await building; Assert.Null(vm.Plan); Assert.False(vm.CanRun); Assert.Empty(jobs.Items);
    }
    [Fact]
    public async Task Running_copy_uses_its_snapshot_and_only_records_successful_outputs()
    {
        var vm = Model(); Add(); input.SetSelection([input.VisibleFiles[0]]);
        await vm.BuildPlanCommand.ExecuteAsync(null); var plan = vm.Plan!;
        var pending = new TaskCompletionSource<CopyReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PendingExecution = pending.Task;
        var running = vm.RunCommand.ExecuteAsync(null);
        input.SetSelection([]); input.AddSearchFiles([new(@"C:\later.txt", false, 10, null)]);
        Assert.Same(plan, service.Executed); Assert.Same(plan, vm.Plan);
        Assert.Contains("固定计划", vm.ScopeSummary);
        pending.SetResult(FakeCopy.Report(plan)); await running;
        Assert.Single(outputs.VisibleFiles); Assert.Equal(3, input.SearchEntries.Count);
        var job = Assert.Single(jobs.Items); Assert.StartsWith("批量复制", job.Name); Assert.False(job.IsRunning);
        var status = job.Status; job.Update(new(0, 1, "late", 1, 10)); Assert.Equal(status, job.Status);
        Assert.Null(vm.Plan); Assert.False(vm.CanRun);
    }
    [Fact]
    public async Task Retry_builds_a_reviewable_plan_without_repeating_successes_or_explicit_skips()
    {
        var vm = Model(); Add(); await vm.BuildPlanCommand.ExecuteAsync(null);
        var plan = vm.Plan!;
        service.PendingExecution = Task.FromResult(new CopyReport(plan,
            [new(plan.Items[0], CopyOutcome.Skipped, "user skip"), new(plan.Items[1], CopyOutcome.Failed, "locked")], false));
        await vm.RunCommand.ExecuteAsync(null);
        Assert.True(vm.CanRetry);
        vm.Destination = @"D:\later-choice";
        await vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(@"D:\out", vm.Destination);
        Assert.Single(vm.Plan!.Items); Assert.True(vm.CanRun); Assert.Single(jobs.Items);
        service.PendingExecution = null;
        await vm.RunCommand.ExecuteAsync(null);
        Assert.Single(outputs.VisibleFiles); Assert.False(vm.CanRetry); Assert.Equal(2, vm.Report!.Items.Count);
        Assert.Equal(CopyOutcome.Skipped, vm.Report.Items[0].Outcome);
    }
    private sealed class Interaction : IFileInteraction
    { public void Open(SearchFile file) { } public void Reveal(SearchFile file) { } public void CopyText(string text) { } }
    private sealed class FakeCopy : ICopyService
    {
        public CopyRequest? Request;
        public CopyPlan? Executed;
        public Task<CopyPlan>? PendingPlan;
        public Task<CopyReport>? PendingExecution;
        public static CopyPlan Plan(CopyRequest request) => new(request, request.Paths.Select(p => new CopyPlanItem(Guid.NewGuid(),
            new(p, "", "", false, new(10, 1, p)), Path.Combine(request.Destination, Path.GetFileName(p)), CopyAction.Copy)).ToArray());
        public static CopyReport Report(CopyPlan plan) => new(plan, plan.Items.Select(x => new CopyItemResult(x, CopyOutcome.Success, "done",
            new ProducedFile(x.Target, 10, DateTimeOffset.UtcNow, [x.Source.Path]))).ToArray(), false);
        public Task<CopyPlan> PlanAsync(CopyRequest request, IProgress<ProcessingProgress>? progress, CancellationToken token)
        { Request = request; return PendingPlan ?? Task.FromResult(Plan(request)); }
        public Task<CopyReport> ExecuteAsync(CopyPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token)
        { Executed = plan; return PendingExecution ?? Task.FromResult(Report(plan)); }
        public Task<CopyPlan> RetryPlanAsync(CopyReport report, CancellationToken token) => Task.FromResult(report.Plan with { Items = report.Items.Where(x => x.Retryable).Select(x => x.Item).ToArray() });
        public Task<CopyPlan> DecideAsync(CopyPlan plan, Guid item, CopyDecision decision, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<CopyRecovery>> RecoveriesAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<CopyRecovery>>([]);
        public Task RestoreAsync(Guid recovery, CancellationToken token) => Task.CompletedTask;
        public Task DiscardBackupAsync(Guid recovery, CancellationToken token) => Task.CompletedTask;
    }
}
