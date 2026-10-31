using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.App.Controls;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Engines;

namespace FileAssistant.App.Features.Convert;

public partial class ConversionViewModel : ObservableObject
{
    private readonly IFileProcessingService service;
    private readonly OutputsViewModel outputs;
    private readonly ProcessingJobsViewModel jobs;
    private CancellationTokenSource? cancellation;
    private string inputIdentity = string.Empty;
    private int optionsRevision;
    public WorkspaceViewModel Input { get; }
    public static IReadOnlyList<SelectionOption> Formats { get; } =
        [new(0,"PNG"),new(1,"JPEG"),new(2,"WebP"),new(3,"BMP"),new(4,"PDF · 图片合并")];
    public ConversionViewModel(IFileProcessingService service, WorkspaceViewModel input, OutputsViewModel outputs, ProcessingJobsViewModel jobs)
    {
        this.service = service; Input = input; this.outputs = outputs; this.jobs = jobs;
        input.PropertyChanged += InputChanged;
    }
    [ObservableProperty] public partial int FormatIndex { get; set; }
    [ObservableProperty] public partial double Quality { get; set; } = 90;
    [ObservableProperty] public partial double PdfDpi { get; set; } = 150;
    [ObservableProperty] public partial string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileAssistant输出");
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRun), nameof(PlanSummary)), NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial ConversionPlan? Plan { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanRun)), NotifyCanExecuteChangedFor(nameof(BuildPlanCommand), nameof(RunCommand), nameof(CancelCommand))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ShowOutputsCommand))] public partial ConversionReport? Report { get; set; }
    public bool CanEdit => !IsBusy;
    public bool CanRun => !IsBusy && Plan?.ReadyCount > 0;
    private bool CanShowOutputs => Report?.Outputs.Count > 0;
    public string PlanSummary => Plan is null ? "先预览转换计划，再开始转换。" : $"将生成 {Plan.ReadyCount} 个文件 · {Plan.RejectedCount} 项不可处理";
    public event EventHandler<int>? OutputsRequested;
    private int lastTask;
    public string InputIdentity => string.Join('\0', Input.SearchEntries.Select(file => file.FullPath));
    private void InputChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceViewModel.BatchSummary)) return;
        var identity = InputIdentity;
        if (identity != inputIdentity) { inputIdentity = identity; Invalidate(); }
    }
    private void Invalidate() { optionsRevision++; Plan = null; }
    partial void OnFormatIndexChanged(int value) => Invalidate();
    partial void OnQualityChanged(double value) => Invalidate();
    partial void OnPdfDpiChanged(double value) => Invalidate();
    partial void OnOutputDirectoryChanged(string value) => Invalidate();

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task BuildPlanAsync()
    {
        var identity = InputIdentity;
        var revision = optionsRevision;
        if (!double.IsFinite(Quality) || !double.IsFinite(PdfDpi)) { Feedback = "请填写有效的质量和 PDF 分辨率。"; return; }
        IsBusy = true; Plan = null; Feedback = "正在检查文件和输出路径…";
        using var current = new CancellationTokenSource(); cancellation = current;
        try
        {
            var result = await service.CreatePlanAsync(new(Input.SearchEntries.Select(file => file.FullPath).ToArray(),
                OutputDirectory.Trim(), (ConversionTarget)FormatIndex, (int)Quality, (int)PdfDpi), current.Token);
            if (identity != InputIdentity || revision != optionsRevision) { Feedback = "输入文件或参数已变化，请重新预览计划。"; return; }
            Plan = result; Feedback = "计划已就绪，已有同名文件将自动编号保留。";
        }
        catch (OperationCanceledException) { Feedback = "已取消预览计划。"; }
        catch (FileProcessingException error) { Feedback = error.Message; }
        catch (Exception) { Feedback = "无法生成转换计划，请检查输入文件和输出路径。"; }
        finally { if (ReferenceEquals(cancellation,current)) cancellation = null; IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (Plan is not { } plan) return;
        IsBusy = true; Report = null;
        using var current = new CancellationTokenSource(); cancellation = current;
        var job = jobs.Start(current.Cancel); lastTask = job.Id;
        Feedback = "正在转换，原文件保留…";
        try
        {
            var report = await service.ConvertAsync(plan, new Progress<ProcessingProgress>(job.Update), current.Token);
            Report = report; outputs.Add(job.Id, job.Name, report.Outputs); job.Complete(report); Feedback = report.Summary;
        }
        catch (OperationCanceledException) { job.Fail("已取消。"); Feedback = "已取消转换。"; }
        catch (Exception error) { job.Fail(error.Message); Feedback = "转换未完成，请查看任务明细。"; }
        finally { if (ReferenceEquals(cancellation,current)) cancellation = null; Plan = null; IsBusy = false; }
    }
    [RelayCommand(CanExecute = nameof(IsBusy))] private void Cancel() => cancellation?.Cancel();
    [RelayCommand(CanExecute = nameof(CanShowOutputs))] private void ShowOutputs() => OutputsRequested?.Invoke(this, lastTask);
}
