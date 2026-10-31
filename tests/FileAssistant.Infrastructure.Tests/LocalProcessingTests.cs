using System.Diagnostics;
using System.Security.Cryptography;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;
using ImageMagick;

namespace FileAssistant.Infrastructure.Tests;

public sealed class LocalProcessingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-engine-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileProcessingService service;
    private readonly string ffmpeg;

    public LocalProcessingTests()
    {
        Directory.CreateDirectory(root);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "FileAssistant.slnx"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("Test repository not found.");
        ffmpeg = Path.Combine(repository.FullName, "artifacts", "dependencies", "ffmpeg", "8.1.3-9-g29e619e767", "x64", "bundle", "bin", "ffmpeg.exe");
        service = new(new(ffmpeg, Path.Combine(root, "cache")));
    }

    private string Image(string name, IMagickColor<ushort>? color = null)
    {
        var path = Path.Combine(root, name);
        using var image = new MagickImage(color ?? MagickColors.Red, 80, 40);
        image.Write(path, MagickFormat.Png);
        return path;
    }

    [Fact]
    public async Task Preview_and_all_image_targets_are_real_files_and_keep_originals()
    {
        var source = Image("中文 空格 & 原件.png");
        var original = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var preview = await service.PreviewAsync(source, 256, default);
        Assert.Equal(PreviewKind.Image, preview.Kind);
        using (var decoded = new MagickImage(preview.Png!)) { Assert.Equal(80u, decoded.Width); Assert.Equal(40u, decoded.Height); }
        foreach (var target in new[] { ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Bmp })
        {
            var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, target.ToString()), target), default);
            Assert.False(Directory.Exists(plan.Request.OutputDirectory));
            var report = await service.ConvertAsync(plan, null, default);
            var output = Assert.Single(report.Outputs);
            using var decoded = new MagickImage(output.FullPath);
            Assert.Equal(80u, decoded.Width);
            Assert.Equal(40u, decoded.Height);
            Assert.NotEqual(source, output.FullPath);
        }
        Assert.Equal(original, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    [Fact]
    public async Task Images_to_pdf_and_pdf_to_pages_share_the_local_pdf_engine()
    {
        var first = Image("first.png");
        var second = Image("second.png", MagickColors.Blue);
        var pdfPlan = await service.CreatePlanAsync(new([first, second], Path.Combine(root, "pdf"), ConversionTarget.Pdf), default);
        var pdf = Assert.Single((await service.ConvertAsync(pdfPlan, null, default)).Outputs);
        Assert.Equal(2, pdf.SourcePaths.Count);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(pdf.FullPath)).Take(4).ToArray()));
        var preview = await service.PreviewAsync(pdf.FullPath, 300, default);
        Assert.Equal(PreviewKind.Pdf, preview.Kind);
        using (var image = new MagickImage(preview.Png!)) Assert.InRange(image.Width, 1u, 300u);
        var export = await service.CreatePlanAsync(new([pdf.FullPath], Path.Combine(root, "pages"), ConversionTarget.Png, PdfDpi: 72), default);
        Assert.Equal(2, export.ReadyCount);
        var report = await service.ConvertAsync(export, null, default);
        Assert.Equal(2, report.Outputs.Count);
        for (var index = 0; index < report.Outputs.Count; index++)
        {
            using var image = new MagickImage(report.Outputs[index].FullPath);
            using var pixels = image.GetPixels();
            var center = pixels.GetPixel((int)image.Width / 2, (int)image.Height / 2).ToColor()!;
            Assert.True(index == 0 ? center.R > center.B * 2 : center.B > center.R * 2);
        }
    }

    [Fact]
    public async Task Output_collision_and_input_changes_after_preview_never_overwrite_files()
    {
        var source = Image("same.png");
        var original = await File.ReadAllBytesAsync(source);
        var plan = await service.CreatePlanAsync(new([source], root, ConversionTarget.Png), default);
        var item = Assert.Single(plan.Items);
        Assert.NotEqual(source, item.OutputPath);
        await File.WriteAllTextAsync(item.OutputPath!, "created after preview");
        var report = await service.ConvertAsync(plan, null, default);
        Assert.Empty(report.Outputs);
        Assert.Equal("created after preview", await File.ReadAllTextAsync(item.OutputPath!));
        Assert.Equal(original, await File.ReadAllBytesAsync(source));
        var next = await service.CreatePlanAsync(new([source], Path.Combine(root, "changed"), ConversionTarget.Jpeg), default);
        await File.AppendAllTextAsync(source, "changed");
        Assert.Empty((await service.ConvertAsync(next, null, default)).Outputs);
        Assert.False(Directory.Exists(next.Request.OutputDirectory));
    }

    [Fact]
    public async Task Bad_inputs_and_cancellation_preserve_completed_outputs_without_temp_files()
    {
        var first = Image("one.png");
        var second = Image("two.png");
        var unsupported = Path.Combine(root, "not-an-image.txt");
        await File.WriteAllTextAsync(unsupported, "text");
        var plan = await service.CreatePlanAsync(new([first, unsupported, second], Path.Combine(root, "cancel"), ConversionTarget.WebP), default);
        Assert.Equal(1, plan.RejectedCount);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress(value => { if (value.Completed == 1) cancellation.Cancel(); });
        var report = await service.ConvertAsync(plan, progress, cancellation.Token);
        Assert.True(report.Cancelled);
        Assert.Single(report.Outputs);
        Assert.Empty(Directory.GetFiles(plan.Request.OutputDirectory, "*.tmp"));
        Assert.True(File.Exists(first)); Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task Video_preview_is_the_first_decoded_frame_and_requires_no_network()
    {
        Assert.True(File.Exists(ffmpeg), "Run scripts/restore-processing-engines.ps1 before engine tests.");
        var video = Path.Combine(root, "首帧 视频.mkv");
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-nostdin", "-v", "error", "-f", "lavfi", "-i", "color=c=red:s=64x32:r=1:d=1", "-f", "lavfi", "-i", "color=c=blue:s=64x32:r=1:d=1", "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0[out]", "-map", "[out]", "-c:v", "ffv1", video }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, await errors);
        var preview = await service.PreviewAsync(video, 256, default);
        Assert.Equal(PreviewKind.Video, preview.Kind);
        using var image = new MagickImage(preview.Png!);
        using var pixels = image.GetPixels();
        var color = pixels.GetPixel(10, 10).ToColor()!;
        Assert.True(color.R > color.B * 2);
    }

    [Fact]
    public async Task Transparent_pixels_become_white_in_jpeg_without_changing_the_source()
    {
        var source = Image("transparent.png", MagickColors.Transparent);
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "white"), ConversionTarget.Jpeg), default);
        var file = Assert.Single((await service.ConvertAsync(plan, null, default)).Outputs);
        using var image = new MagickImage(file.FullPath);
        using var pixels = image.GetPixels();
        var white = pixels.GetPixel(10, 10).ToColor()!;
        Assert.True(white.R > 64_000 && white.G > 64_000 && white.B > 64_000);
        using var original = new MagickImage(source);
        Assert.True(original.HasAlpha);
    }

    [Fact]
    public async Task Remote_paths_and_disguised_image_documents_are_rejected()
    {
        await Assert.ThrowsAsync<FileProcessingException>(() => service.PreviewAsync("https://example.invalid/file.png", 256, default));
        var plan = await service.CreatePlanAsync(new(["https://example.invalid/file.png"], Path.Combine(root, "remote"), ConversionTarget.Png), default);
        Assert.Equal(0, plan.ReadyCount); Assert.Equal(1, plan.RejectedCount);
        var disguised = Path.Combine(root, "actually-svg.png");
        await File.WriteAllTextAsync(disguised, "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.invalid/a.png'/></svg>");
        await Assert.ThrowsAsync<FileProcessingException>(() => service.PreviewAsync(disguised, 256, default));
        Assert.False(Directory.Exists(plan.Request.OutputDirectory));
    }

    private sealed class InlineProgress(Action<ProcessingProgress> report) : IProgress<ProcessingProgress>
    { public void Report(ProcessingProgress value) => report(value); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
