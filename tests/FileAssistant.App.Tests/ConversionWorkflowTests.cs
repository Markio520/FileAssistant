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
    public async Task Processing_mode_filters_same_formats_and_every_parameter_invalidates_plan()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.jpg", false, 1, null)]);
        vm.ModeValue = (int)ConversionMode.Process;
        Assert.Equal([(int)ConversionTarget.Jpeg], vm.Formats.Select(option => option.Value));
        foreach (var change in new Action[] { () => vm.Quality = 70, () => vm.MaximumEdge = 1280, () => vm.PresetValue = 1,
            () => vm.AudioTrack = 2, () => vm.AudioBitrate = 160, () => vm.VideoBitrate = 900, () => vm.SampleRate = 44100,
            () => vm.Frame = 2, () => vm.AllTiffPages = false, () => vm.Sheet = 1, () => vm.EncodingValue = 1,
            () => vm.SeparatorValue = 1, () => vm.ExportFormulas = true, () => vm.ImportFormulas = true })
        {
            await vm.BuildPlanCommand.ExecuteAsync(null); Assert.NotNull(vm.Plan);
            change(); Assert.Null(vm.Plan); Assert.False(vm.CanRun);
        }
        vm.ModeValue = 0; Assert.DoesNotContain(vm.Formats, option => option.Value == (int)ConversionTarget.Jpeg);
    }

    [Fact]
    public async Task Spreadsheet_options_reach_the_plan_and_pdf_text_hides_raster_settings()
    {
        service.Engines = [new("libreoffice", "Office", "test", "documents", true, "test")];
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.xlsx", false, 1, null)]);
        vm.TargetFormatValue = (int)ConversionTarget.Tsv; vm.Sheet = 2; vm.ExportFormulas = true; vm.EncodingValue = 1;
        Assert.True(vm.ShowSheet); await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Request!.Options.Sheet); Assert.True(service.Request.Options.ExportFormulas);
        Assert.Equal(DelimitedEncoding.Gb18030, service.Request.Options.Encoding);
        vm.Sheet = double.NaN; await vm.BuildPlanCommand.ExecuteAsync(null); Assert.Null(vm.Plan); Assert.Contains("有效", vm.Feedback);
        input.AddSearchFiles([new(@"C:\test\text.pdf", false, 1, null)]); input.SetSelection([input.VisibleFiles.Last()]);
        vm.TargetFormatValue = (int)ConversionTarget.Txt; Assert.False(vm.ShowPdfDpi); Assert.False(vm.ShowQuality);
        await vm.BuildPlanCommand.ExecuteAsync(null); Assert.NotNull(vm.Plan);
    }

    [Fact]
    public async Task Missing_optional_engine_has_an_install_entry_and_refresh_enables_document_targets()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.docx", false, 1, null), new(@"C:\test\two.txt", false, 1, null)]);
        Assert.True(vm.HasMissingEngine); Assert.Empty(vm.Formats); Assert.False(vm.CanBuildPlan);
        var requested = false; vm.EngineManagementRequested += (_, _) => requested = true;
        vm.OpenEngineManagementCommand.Execute(null); Assert.True(requested);
        service.Engines = [new("libreoffice", "Office", "test", "documents", true, "test")];
        vm.RefreshCapabilities();
        Assert.False(vm.HasMissingEngine); Assert.True(vm.CanBuildPlan);
        Assert.Equal([(int)ConversionTarget.Pdf, (int)ConversionTarget.Doc, (int)ConversionTarget.Odt, (int)ConversionTarget.Rtf], vm.Formats.Select(option => option.Value));
        Assert.Equal("PDF", vm.Formats[0].Label);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Request!.Paths.Count);
    }

    [Fact]
    public void Media_menus_require_the_engine_and_do_not_mix_audio_with_video()
    {
        service.Engines = [new("ffmpeg", "FFmpeg", "test", "media", true, "test")];
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.mp3", false, 1, null), new(@"C:\test\two.wav", false, 1, null)]);
        Assert.Equal([(int)ConversionTarget.Flac, (int)ConversionTarget.M4a, (int)ConversionTarget.Aac, (int)ConversionTarget.Ogg], vm.Formats.Select(option => option.Value));
        input.AddSearchFiles([new(@"C:\test\three.mp4", false, 1, null)]);
        Assert.Empty(vm.Formats); Assert.False(vm.HasMissingEngine);
        input.SetSelection([input.VisibleFiles[2]]);
        Assert.Equal([(int)ConversionTarget.Mp3, (int)ConversionTarget.Wav, (int)ConversionTarget.Flac, (int)ConversionTarget.M4a, (int)ConversionTarget.Mkv, (int)ConversionTarget.Mov, (int)ConversionTarget.WebM], vm.Formats.Select(option => option.Value));
    }

    [Fact]
    public async Task Empty_and_unsupported_inputs_cannot_build_or_execute_a_plan()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        Assert.Empty(vm.Formats);
        Assert.False(vm.CanBuildPlan);
        Assert.Equal(-1, vm.TargetFormatValue);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Null(service.Request);
        input.AddSearchFiles([new(@"C:\test\archive.zip", false, 1, null)]);
        Assert.Empty(vm.Formats);
        Assert.True(vm.HasFormatError);
        Assert.Contains("ZIP", vm.FormatFeedback);
        Assert.False(vm.BuildPlanCommand.CanExecute(null));
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Null(service.Request);
        Assert.Empty(jobs.Items);
    }

    [Fact]
    public async Task Mixed_images_use_stable_target_values_and_preserve_a_still_valid_choice()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.png", false, 1, null)]);
        Assert.DoesNotContain(vm.Formats, option => option.Value == (int)ConversionTarget.Png);
        Assert.Equal((int)ConversionTarget.Jpeg, vm.TargetFormatValue);
        vm.TargetFormatValue = (int)ConversionTarget.Bmp;
        input.AddSearchFiles([new(@"C:\test\two.webp", false, 1, null)]);
        Assert.Equal([(int)ConversionTarget.Jpeg, (int)ConversionTarget.Bmp, (int)ConversionTarget.Pdf, (int)ConversionTarget.Tiff, (int)ConversionTarget.Gif, (int)ConversionTarget.Avif], vm.Formats.Select(option => option.Value));
        Assert.Equal((int)ConversionTarget.Bmp, vm.TargetFormatValue);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal(ConversionTarget.Bmp, service.Request!.Target);
        Assert.Equal(2, service.Request.Paths.Count);
        input.AddSearchFiles([new(@"C:\test\three.bmp", false, 1, null)]);
        Assert.Equal((int)ConversionTarget.Jpeg, vm.TargetFormatValue);
        Assert.Null(vm.Plan);
    }

    [Fact]
    public async Task Selected_images_ignore_unselected_documents_and_keep_input_list_order()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.png", false, 1, null), new(@"C:\test\notes.docx", false, 1, null),
            new(@"C:\test\two.webp", false, 1, null)]);
        Assert.Empty(vm.Formats);
        Assert.Contains("分开选择", vm.FormatFeedback);
        var first = input.VisibleFiles[0]; var second = input.VisibleFiles[2];
        input.SetSelection([second, first]);
        Assert.True(vm.CanBuildPlan);
        Assert.Equal("仅转换已选 2 项。", vm.ScopeSummary);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal([first.FullPath, second.FullPath], service.Request!.Paths);
        Assert.Equal(ConversionTarget.Jpeg, service.Request.Target);
        await vm.RunCommand.ExecuteAsync(null);
        Assert.Equal([first.FullPath, second.FullPath], Assert.Single(outputs.VisibleFiles).Result.SourcePaths);
        input.SetSelection([]);
        Assert.Empty(vm.Formats);
        Assert.Null(vm.Plan);
        Assert.False(vm.CanRun);
        Assert.Contains("全部 3 项", vm.ScopeSummary);
    }

    [Fact]
    public async Task Only_parameters_relevant_to_the_selected_conversion_are_used()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\page.pdf", false, 1, null)]);
        Assert.True(vm.ShowPdfDpi);
        Assert.False(vm.ShowQuality);
        Assert.DoesNotContain(vm.Formats, option => option.Value == (int)ConversionTarget.Pdf);
        input.ClearCommand.Execute(null);
        input.AddSearchFiles([new(@"C:\test\image.png", false, 1, null)]);
        vm.TargetFormatValue = (int)ConversionTarget.Bmp;
        vm.Quality = double.NaN; vm.PdfDpi = double.NaN;
        Assert.False(vm.ShowQuality);
        Assert.False(vm.ShowPdfDpi);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.True(vm.CanRun);
        Assert.Equal(90, service.Request!.Quality);
        Assert.Equal(150, service.Request.PdfDpi);
    }

    [Fact]
    public async Task Changing_selection_with_the_same_count_invalidates_the_plan()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.png", false, 1, null), new(@"C:\test\two.png", false, 1, null)]);
        var first = input.VisibleFiles[0]; var second = input.VisibleFiles[1];
        input.SetSelection([first]);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.True(vm.CanRun);
        input.SetSelection([second]);
        Assert.Null(vm.Plan);
        Assert.False(vm.CanRun);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal([second.FullPath], service.Request!.Paths);
    }

    [Fact]
    public async Task A_plan_for_an_old_selection_is_discarded_even_when_the_input_list_is_unchanged()
    {
        var pending = new TaskCompletionSource<ConversionPlan>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Pending = pending.Task;
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.png", false, 1, null), new(@"C:\test\two.png", false, 1, null)]);
        var first = input.VisibleFiles[0]; var second = input.VisibleFiles[1];
        input.SetSelection([first]);
        var building = vm.BuildPlanCommand.ExecuteAsync(null);
        input.SetSelection([second]);
        pending.SetResult(FakeProcessing.Plan(service.Request!));
        await building;
        Assert.Null(vm.Plan);
        Assert.False(vm.CanRun);
        Assert.Contains("已变化", vm.Feedback);
    }

    [Fact]
    public async Task Folder_inputs_and_targets_absent_from_the_menu_cannot_start_processing()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\folder.png", true, null, null)]);
        Assert.Empty(vm.Formats);
        Assert.Contains("文件夹", vm.FormatFeedback);
        input.ClearCommand.Execute(null);
        input.AddSearchFiles([new(@"C:\test\image.png", false, 1, null)]);
        vm.TargetFormatValue = (int)ConversionTarget.Png;
        Assert.False(vm.CanBuildPlan);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Null(service.Request);
    }

    [Fact]
    public async Task Clearing_selection_falls_back_to_all_inputs_and_clearing_the_list_removes_old_selection()
    {
        var vm = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\one.png", false, 1, null), new(@"C:\test\two.png", false, 1, null)]);
        input.SetSelection([input.VisibleFiles[0]]);
        input.SearchText = "one";
        Assert.Empty(input.SelectedEntries);
        Assert.Contains("全部 2 项", vm.ScopeSummary);
        await vm.BuildPlanCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Request!.Paths.Count);
        input.ClearCommand.Execute(null);
        Assert.Empty(input.SelectedEntries);
        Assert.Empty(vm.Formats);
        Assert.False(vm.CanRun);
    }

    [Fact]
    public async Task Plans_become_invalid_when_inputs_or_conversion_options_change()
    {
        var viewModel = new ConversionViewModel(service, input, outputs, jobs);
        input.AddSearchFiles([new(@"C:\test\a.png", false, 1, null)]);
        await viewModel.BuildPlanCommand.ExecuteAsync(null);
        Assert.True(viewModel.CanRun);
        input.SearchText = "different display filter";
        Assert.True(viewModel.CanRun); // Display filtering must not change the input set.
        viewModel.TargetFormatValue = (int)ConversionTarget.WebP;
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
        public IReadOnlyList<ProcessingEngineInfo> Engines { get; set; } = [];
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
