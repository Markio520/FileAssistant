using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.App.Controls;
using FileAssistant.App.Features.Outputs;
using FileAssistant.App.Features.Tasks;
using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Convert;

public partial class ConversionViewModel : ObservableObject
{
    private readonly IFileProcessingService service;
    private readonly OutputsViewModel outputs;
    private readonly ProcessingJobsViewModel jobs;
    private CancellationTokenSource? cancellation;
    private string inputIdentity = string.Empty;
    private int optionsRevision;
    private HashSet<string> availableEngines = [];
    public WorkspaceViewModel Input { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasFormats), nameof(CanSelectFormat), nameof(CanBuildPlan), nameof(CanRun), nameof(FormatButtonContent))]
    [NotifyCanExecuteChangedFor(nameof(BuildPlanCommand), nameof(RunCommand))]
    public partial IReadOnlyList<SelectionOption> Formats { get; set; } = [];
    public ConversionViewModel(IFileProcessingService service, WorkspaceViewModel input, OutputsViewModel outputs, ProcessingJobsViewModel jobs)
    {
        this.service = service; Input = input; this.outputs = outputs; this.jobs = jobs;
        input.PropertyChanged += InputChanged;
        RefreshCapabilities();
    }
    // A stable ConversionTarget value, never the position in the filtered menu.
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanBuildPlan), nameof(CanRun), nameof(ShowQuality), nameof(ShowPdfDpi), nameof(ConversionNote))]
    [NotifyCanExecuteChangedFor(nameof(BuildPlanCommand), nameof(RunCommand))]
    public partial int TargetFormatValue { get; set; } = -1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasFormatError))] public partial string FormatFeedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormatButtonContent))] public partial bool HasMissingEngine { get; set; }
    [ObservableProperty] public partial double Quality { get; set; } = 90;
    [ObservableProperty] public partial double PdfDpi { get; set; } = 150;
    [ObservableProperty] public partial string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileAssistant输出");
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRun), nameof(PlanSummary)), NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial ConversionPlan? Plan { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanSelectFormat), nameof(CanBuildPlan), nameof(CanRun)), NotifyCanExecuteChangedFor(nameof(BuildPlanCommand), nameof(RunCommand), nameof(CancelCommand))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ShowOutputsCommand))] public partial ConversionReport? Report { get; set; }
    public bool CanEdit => !IsBusy;
    public bool HasFormats => Formats.Count > 0;
    public bool CanSelectFormat => CanEdit && HasFormats;
    public bool CanBuildPlan => CanSelectFormat && Formats.Any(option => option.Value == TargetFormatValue);
    public bool CanRun => CanBuildPlan && Plan?.ReadyCount > 0;
    public bool HasFormatError => FormatFeedback.Length > 0;
    public string? FormatButtonContent => HasFormats ? null : HasMissingEngine ? "请先安装所需引擎" : ConversionEntries.Count == 0 ? "请先添加文件" : "无可用输出格式";
    public bool ShowQuality => TargetFormatValue is (int)ConversionTarget.Jpeg or (int)ConversionTarget.WebP or (int)ConversionTarget.Avif;
    public bool ShowPdfDpi => HasFormats && ConversionEntries.Any(file => file.Extension.Equals("pdf", StringComparison.OrdinalIgnoreCase));
    public string ConversionNote => ConversionEntries.Any(file => ConversionCapabilities.IsDocument(file.FullPath))
        ? "每份文档单独输出；字体和复杂排版可能影响分页。TXT 使用 UTF-8，输出 TXT 只保留文本。启动文档引擎可能需要较长时间，可随时取消。"
        : ConversionEntries.Any(file => ConversionCapabilities.IsVideo(file.FullPath))
            ? "转换首个视频流与首个音轨，字幕和其他轨道不输出。MP4/MKV/MOV 使用 H.264 + AAC；WebM 使用 VP9 + Opus。"
        : ConversionEntries.Any(file => ConversionCapabilities.IsAudio(file.FullPath)) ? "转换首个音轨。WAV 使用 16 位 PCM；有损音源转 FLAC/WAV 不会恢复已丢失的音质。"
        : TargetFormatValue == (int)ConversionTarget.Pdf ? "按输入列表顺序合并为一个 PDF，一图一页；多帧图片读取第一帧。"
        : TargetFormatValue is (int)ConversionTarget.Jpeg or (int)ConversionTarget.Bmp ? "透明区域将使用白色背景。多帧图片读取第一帧。"
        : "多帧图片只读取第一帧；GIF 输出为静态图片。";
    public string ScopeSummary => ConversionEntries.Count == 0 ? Input.SelectedFolderIds.Count > 0 ? "所选文件夹为空，请重新选择。" : "当前文件夹没有文件，请先添加要转换的文件。"
        : Input.SelectionCount > 0 ? Input.SelectedFolderIds.Count > 0 ? $"已选 {Input.SelectedFolderIds.Count:N0} 个文件夹" + (Input.SelectionCount > Input.SelectedFolderIds.Count ? $"、{Input.SelectionCount - Input.SelectedFolderIds.Count:N0} 个文件" : "") + $" · 实际转换 {ConversionEntries.Count:N0} 项（含子文件夹，已去重）。" : $"仅转换已选 {ConversionEntries.Count:N0} 项。"
        : Input.CurrentFolderId != Guid.Empty ? $"将转换“{Input.CurrentFolderName}”及子文件夹中的全部 {ConversionEntries.Count:N0} 个文件。"
        : $"未选择文件，将转换输入列表全部 {ConversionEntries.Count:N0} 项。";
    private bool CanShowOutputs => Report?.Outputs.Count > 0;
    public string PlanSummary => Plan is null ? "先预览转换计划，再开始转换。" : $"将生成 {Plan.ReadyCount} 个文件 · {Plan.RejectedCount} 项不可处理";
    public event EventHandler<int>? OutputsRequested;
    public event EventHandler? EngineManagementRequested;
    private int lastTask;
    private IReadOnlyList<SearchFile> ConversionEntries => Input.EffectiveEntries;
    public string InputIdentity => $"{Input.CurrentFolderId:N}:{Input.CollectionRevision}:" + string.Join('\0', ConversionEntries.Select(file => file.FullPath));
    private void InputChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(WorkspaceViewModel.BatchSummary) or nameof(WorkspaceViewModel.EffectiveEntries) or nameof(WorkspaceViewModel.CollectionRevision))) return;
        RefreshInput();
    }
    private void RefreshInput(bool force = false)
    {
        OnPropertyChanged(nameof(ScopeSummary));
        var identity = InputIdentity;
        if (!force && identity == inputIdentity) return;
        inputIdentity = identity;
        Invalidate();
        var entries = ConversionEntries;
        IReadOnlyList<ConversionTarget> targets = entries.Any(file => file.IsFolder) ? [] : ConversionCapabilities.GetCommonTargets(entries.Select(file => file.FullPath));
        var requiredEngine = entries.Any(file => ConversionCapabilities.IsDocument(file.FullPath)) ? "libreoffice"
            : entries.Any(file => ConversionCapabilities.IsAudio(file.FullPath) || ConversionCapabilities.IsVideo(file.FullPath)) ? "ffmpeg" : null;
        HasMissingEngine = targets.Count > 0 && requiredEngine is not null && !availableEngines.Contains(requiredEngine);
        if (HasMissingEngine) targets = [];
        Formats = targets.Select(target => new SelectionOption((int)target, target switch
        {
            ConversionTarget.Jpeg => "JPG / JPEG", ConversionTarget.WebP => "WebP", ConversionTarget.WebM => "WebM",
            ConversionTarget.Pdf => entries.All(file => ConversionCapabilities.IsImage(file.FullPath)) ? "PDF · 图片合并" : "PDF", _ => target.ToString().ToUpperInvariant()
        })).ToArray();
        if (!Formats.Any(option => option.Value == TargetFormatValue)) TargetFormatValue = Formats.FirstOrDefault()?.Value ?? -1;
        FormatFeedback = string.Empty;
        if (HasMissingEngine) FormatFeedback = requiredEngine == "libreoffice" ? "文档转换需要 LibreOffice，请前往引擎管理按需下载。文件将在本机转换。" : "音视频引擎缺失，请前往引擎管理下载或修复。";
        else if (entries.Any(file => file.IsFolder)) FormatFeedback = "当前不直接转换磁盘文件夹，请选择其中的文件；软件文件夹会自动展开已收集的文件引用。";
        else if (entries.Count > 0 && !HasFormats)
        {
            var unsupported = entries.Where(file => ConversionCapabilities.GetTargets(file.FullPath).Count == 0)
                .Select(file => file.Extension.Length == 0 ? "无扩展名文件" : file.Extension.ToUpperInvariant()).Distinct().ToArray();
            var kinds = entries.GroupBy(file => ConversionCapabilities.IsImage(file.FullPath) ? "图片" : ConversionCapabilities.IsDocument(file.FullPath) ? "文本文档"
                : ConversionCapabilities.IsAudio(file.FullPath) ? "音频" : ConversionCapabilities.IsVideo(file.FullPath) ? "视频" : file.Extension.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "其他文件").ToArray();
            var scope = string.Join("、", kinds.Select(group => $"{group.Count():N0} 项{group.Key}"));
            FormatFeedback = $"当前包含 {scope}，没有共同的输出格式，请按类型分开选择。";
            if (unsupported.Length > 0) FormatFeedback += $" 当前尚未接入：{string.Join("、", unsupported)}。";
        }
        OnPropertyChanged(nameof(ShowPdfDpi));
        OnPropertyChanged(nameof(ConversionNote));
    }
    public void RefreshCapabilities()
    {
        var current = service.Engines.Where(engine => engine.IsAvailable).Select(engine => engine.Id).ToHashSet(StringComparer.Ordinal);
        var changed = !availableEngines.SetEquals(current);
        availableEngines = current;
        RefreshInput(force: changed);
    }
    [RelayCommand] private void OpenEngineManagement() => EngineManagementRequested?.Invoke(this, EventArgs.Empty);
    private void Invalidate() { optionsRevision++; Plan = null; }
    partial void OnTargetFormatValueChanged(int value) => Invalidate();
    partial void OnQualityChanged(double value) => Invalidate();
    partial void OnPdfDpiChanged(double value) => Invalidate();
    partial void OnOutputDirectoryChanged(string value) => Invalidate();

    [RelayCommand(CanExecute = nameof(CanBuildPlan))]
    private async Task BuildPlanAsync()
    {
        if (!CanBuildPlan) { Feedback = HasFormatError ? FormatFeedback : "请先添加文件，并选择这批文件共同支持的输出格式。"; return; }
        var entries = ConversionEntries;
        var identity = InputIdentity;
        var revision = optionsRevision;
        var quality = ShowQuality ? Quality : 90;
        var pdfDpi = ShowPdfDpi ? PdfDpi : 150;
        if (!double.IsFinite(quality) || !double.IsFinite(pdfDpi)) { Feedback = "请填写有效的质量和 PDF 分辨率。"; return; }
        IsBusy = true; Plan = null; Feedback = "正在检查文件和输出路径…";
        using var current = new CancellationTokenSource(); cancellation = current;
        try
        {
            var result = await service.CreatePlanAsync(new(entries.Select(file => file.FullPath).ToArray(),
                OutputDirectory.Trim(), (ConversionTarget)TargetFormatValue, (int)quality, (int)pdfDpi), current.Token);
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
        if (!CanRun || Plan is not { } plan) return;
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
