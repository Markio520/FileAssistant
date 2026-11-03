using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.App.Controls;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Organize;

namespace FileAssistant.App.Features.Organize;

public partial class CopyViewModel : ObservableObject
{
    private readonly ICopyService service;
    private readonly OutputsViewModel outputs;
    private readonly ProcessingJobsViewModel jobs;
    private CancellationTokenSource? cancellation;
    private int revision, lastTask;
    private string identity = "";
    private CopyReport? retryBase;
    private TaskCompletionSource? idle;
    public Task WaitForIdleAsync() => idle?.Task ?? Task.CompletedTask;
    public WorkspaceViewModel Input { get; }
    public CopyViewModel(ICopyService service, WorkspaceViewModel input, OutputsViewModel outputs, ProcessingJobsViewModel jobs)
    {
        this.service = service; Input = input; this.outputs = outputs; this.jobs = jobs;
        identity = InputIdentity;
        input.PropertyChanged += InputChanged;
    }
    public IReadOnlyList<SelectionOption> Layouts { get; } = [new(0, "保留目录结构"), new(1, "全部平铺"), new(2, "按扩展名整理")];
    public IReadOnlyList<SelectionOption> Conflicts => WorkspaceSelectionOptions.Conflicts;
    [ObservableProperty] public partial string Destination { get; set; } = "";
    [ObservableProperty] public partial int LayoutValue { get; set; }
    [ObservableProperty] public partial int ConflictValue { get; set; }
    [ObservableProperty] public partial CopyPlan? Plan { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDecision), nameof(CanApproveOverwrite))]
    public partial CopyPlanItem? SelectedPlanItem { get; set; }
    [ObservableProperty] public partial CopyReport? Report { get; set; }
    [ObservableProperty] public partial IReadOnlyList<CopyRecovery> Recoveries { get; set; } = [];
    [ObservableProperty] public partial string Feedback { get; set; } = "选择目标目录后，自动扫描并预览复制计划。";
    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial bool IsResolving { get; set; }
    [ObservableProperty] public partial ProcessingJob? ActiveJob { get; set; }
    public bool IsBusy => IsScanning || IsRunning || IsResolving;
    public bool CanCancel => IsScanning || IsRunning;
    public bool CanEdit => !IsRunning && !IsResolving;
    public bool CanBuild => !IsBusy && !Input.IsLoading && !Input.HasSamples && Input.EffectiveEntries.Count > 0 && !string.IsNullOrWhiteSpace(Destination);
    public bool CanRun => !IsBusy && Plan is { PendingCount: 0, ReadyCount: > 0 };
    public bool CanRetry => !IsBusy && Report?.Items.Any(x => x.Retryable) == true;
    public bool HasOutputs => Report?.Outputs.Count > 0;
    public bool HasRecoveries => Recoveries.Count > 0;
    public bool NoRecoveries => !HasRecoveries;
    public bool CanRecover => !IsBusy && HasRecoveries;
    public bool HasResults => Report is not null;
    public bool HasDecision => !IsBusy && SelectedPlanItem?.NeedsDecision == true;
    public bool CanApproveOverwrite => HasDecision && SelectedPlanItem?.CanOverwrite == true;
    public string InputIdentity => $"{Input.CurrentFolderId}:{Input.CollectionRevision}:" + string.Join('\0', Input.EffectiveEntries.Select(x => x.FullPath));
    public string ScopeSummary => (IsRunning || retryBase is not null) && Plan is { } running
        ? $"本次固定计划 · {running.FileCount:N0} 个文件 · {WorkspaceFile.FormatSize(running.TotalBytes)}"
        : (Input.SelectionCount > 0 ? $"勾选范围 · {Input.SelectionCount:N0} 项（当前可见 {Input.VisibleSelectedCount:N0} 项）" : $"{Input.CurrentFolderName}及子树") +
          (Plan is { } plan ? $"\n{plan.FileCount:N0} 个文件 · {WorkspaceFile.FormatSize(plan.TotalBytes)}" : "\n文件数与总大小将在扫描完成后显示");
    public string PlanSummary => Plan is null ? "尚无可执行计划" :
        $"待处理 {Plan.ReadyCount:N0} · 待确认 {Plan.PendingCount:N0} · 跳过 {Plan.Items.Count(x => x.Action == CopyAction.Skip):N0} · 不可处理 {Plan.Items.Count(x => x.Action == CopyAction.Rejected):N0}";
    public event EventHandler? PlanInvalidated;
    public event EventHandler<int>? OutputsRequested;
    private void InputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.EffectiveEntries) or nameof(WorkspaceViewModel.CollectionRevision) or nameof(WorkspaceViewModel.IsLoading) or nameof(WorkspaceViewModel.BatchSummary))
        { var next = InputIdentity; if (identity != next) { identity = next; Invalidate(); } NotifyState(); }
        if (e.PropertyName is nameof(WorkspaceViewModel.VisibleSelectedCount)) OnPropertyChanged(nameof(ScopeSummary));
    }
    private void Invalidate()
    {
        revision++;
        if (IsScanning) cancellation?.Cancel();
        if (!IsRunning) { Plan = null; retryBase = null; }
        PlanInvalidated?.Invoke(this, EventArgs.Empty);
        NotifyState();
    }
    partial void OnDestinationChanged(string value) => Invalidate();
    partial void OnLayoutValueChanged(int value) => Invalidate();
    partial void OnConflictValueChanged(int value) => Invalidate();
    partial void OnPlanChanged(CopyPlan? value)
    { SelectedPlanItem = value?.Items.FirstOrDefault(x => x.NeedsDecision) ?? value?.Items.FirstOrDefault(); NotifyState(); }
    partial void OnReportChanged(CopyReport? value) => NotifyState();
    partial void OnIsScanningChanged(bool value) => NotifyState();
    partial void OnIsRunningChanged(bool value) => NotifyState();
    partial void OnIsResolvingChanged(bool value) => NotifyState();
    partial void OnRecoveriesChanged(IReadOnlyList<CopyRecovery> value)
    { OnPropertyChanged(nameof(HasRecoveries)); OnPropertyChanged(nameof(NoRecoveries)); OnPropertyChanged(nameof(CanRecover)); }
    private void NotifyState()
    {
        if (IsBusy) idle ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        else { idle?.TrySetResult(); idle = null; }
        foreach (var property in new[] { nameof(IsBusy), nameof(CanEdit), nameof(CanBuild), nameof(CanRun), nameof(CanRetry), nameof(HasOutputs), nameof(HasResults), nameof(ScopeSummary), nameof(PlanSummary) }) OnPropertyChanged(property);
        BuildPlanCommand.NotifyCanExecuteChanged(); RunCommand.NotifyCanExecuteChanged(); RetryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasDecision)); OnPropertyChanged(nameof(CanApproveOverwrite));
        OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(CanRecover));
        CancelCommand.NotifyCanExecuteChanged(); ShowOutputsCommand.NotifyCanExecuteChanged(); ClearSelectionCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildPlanAsync()
    {
        if (!CanBuild) return;
        var currentRevision = revision;
        var request = new CopyRequest(Input.EffectiveEntries.Select(x => x.FullPath).ToArray(), Destination.Trim(), (CopyLayout)LayoutValue, (CopyConflictPolicy)ConflictValue);
        retryBase = null; Plan = null; IsScanning = true;
        using var current = new CancellationTokenSource(); cancellation = current;
        Feedback = "正在扫描文件和目标目录…";
        try
        {
            var plan = await service.PlanAsync(request, new Progress<ProcessingProgress>(p => { if (IsScanning && revision == currentRevision) Feedback = p.Message; }), current.Token);
            if (current.IsCancellationRequested || revision != currentRevision) return;
            Plan = plan;
            Feedback = plan.PendingCount > 0 ? "请逐项处理待确认冲突，然后开始复制。" : plan.ReadyCount > 0 ? "计划已就绪，请核对来源和目标路径。" : "没有待复制项，请查看跳过及不可处理原因。";
        }
        catch (OperationCanceledException) { Feedback = revision == currentRevision ? "扫描已取消，未生成可执行计划。" : "输入或规则已变化，正在更新计划。"; }
        catch (Exception error) { Feedback = "无法生成计划：" + error.Message; }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; IsScanning = false; }
    }
    public async Task DecideAsync(Guid id, CopyDecision decision)
    {
        if (IsBusy || Plan is not { } plan) return;
        var currentRevision = revision; IsResolving = true;
        try { var updated = await service.DecideAsync(plan, id, decision, CancellationToken.None); if (revision == currentRevision) Plan = updated; }
        catch (Exception error) { Feedback = error.Message; }
        finally { IsResolving = false; }
    }
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (!CanRun || Plan is not { } plan) return;
        IsRunning = true;
        using var current = new CancellationTokenSource(); cancellation = current;
        var baseReport = retryBase;
        var job = jobs.Start(current.Cancel, "批量复制"); ActiveJob = job; lastTask = baseReport is null ? job.Id : 0;
        Feedback = "正在复制，输入与规则已固定为本次计划。";
        try
        {
            var result = await service.ExecuteAsync(plan, new Progress<ProcessingProgress>(job.Update), current.Token);
            outputs.Add(job.Id, job.Name, result.Outputs);
            Report = baseReport is null ? result : result with
            {
                Plan = baseReport.Plan with { Items = baseReport.Plan.Items.Select(x => result.Items.FirstOrDefault(n => n.Item.Id == x.Id)?.Item ?? x).ToArray() },
                Items = baseReport.Items.Select(x => result.Items.FirstOrDefault(n => n.Item.Id == x.Item.Id) ?? x).ToArray()
            };
            job.Complete(result.Summary, result.Items.Select(x => $"{x.Item.Source.Path}\n→ {x.Item.Target}\n{x.Message}"), result.Cancelled || result.StopReason is not null);
            Feedback = result.Summary;
        }
        catch (Exception error) { job.Fail(error.Message); Feedback = "复制未完成：" + error.Message; }
        finally
        {
            if (ReferenceEquals(cancellation, current)) cancellation = null;
            Plan = null; retryBase = null; IsRunning = false;
            await RefreshRecoveriesAsync();
        }
    }
    [RelayCommand(CanExecute = nameof(CanRetry))]
    private async Task RetryAsync()
    {
        if (!CanRetry || Report is not { } report) return;
        var currentRevision = revision;
        IsScanning = true; Plan = null;
        using var current = new CancellationTokenSource(); cancellation = current;
        try
        {
            var plan = await service.RetryPlanAsync(report, current.Token);
            if (current.IsCancellationRequested || revision != currentRevision) return;
            Destination = plan.Request.Destination; LayoutValue = (int)plan.Request.Layout; ConflictValue = (int)plan.Request.Conflict;
            retryBase = report; Plan = plan;
            Feedback = "已重新检查未完成项，请核对计划后点击开始复制。成功与主动跳过的项目不会重做。";
        }
        catch (OperationCanceledException) { Feedback = "已取消重试计划。"; }
        catch (Exception error) { Feedback = error.Message; }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; IsScanning = false; }
    }
    [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel() { cancellation?.Cancel(); Feedback = "正在取消，等待当前读写结束并清理临时文件…"; }
    [RelayCommand(CanExecute = nameof(HasOutputs))] private void ShowOutputs() => OutputsRequested?.Invoke(this, lastTask);
    [RelayCommand(CanExecute = nameof(CanEdit))] private void ClearSelection() => Input.SetSelection([]);
    [RelayCommand] private async Task RefreshRecoveriesAsync()
    {
        try { Recoveries = await service.RecoveriesAsync(CancellationToken.None); }
        catch (Exception error) { Feedback = "无法读取覆盖恢复记录：" + error.Message; }
    }
    public async Task RecoverAsync(Guid id, bool discard)
    {
        if (IsBusy) return;
        IsResolving = true;
        try
        {
            if (discard) await service.DiscardBackupAsync(id, CancellationToken.None);
            else await service.RestoreAsync(id, CancellationToken.None);
            Feedback = discard ? "已删除所选备份，当前目标文件保留。" : "已恢复旧目标文件。";
            await RefreshRecoveriesAsync(); Invalidate();
        }
        catch (Exception error) { Feedback = "操作未完成：" + error.Message; }
        finally { IsResolving = false; }
    }
}
