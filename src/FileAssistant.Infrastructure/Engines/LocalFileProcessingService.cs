using System.Security.Cryptography;
using System.Text;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines.Images;
using FileAssistant.Infrastructure.Engines.Documents;
using FileAssistant.Infrastructure.Engines.Media;
using FileAssistant.Infrastructure.Engines.Pdf;
using ImageMagick;

namespace FileAssistant.Infrastructure.Engines;

/// <summary>Shared local engines. Preview caches are never used as conversion inputs.</summary>
public sealed class LocalFileProcessingService(EnginePaths paths) : IFileProcessingService
{
    private readonly SemaphoreSlim previewGate = new(1, 1);
    private readonly SemaphoreSlim conversionGate = new(1, 1);
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".json", ".csv", ".xml", ".xaml", ".yaml", ".yml", ".log", ".cs", ".js", ".ts", ".css", ".html", ".py", ".ini" };

    public IReadOnlyList<ProcessingEngineInfo> Engines =>
    [
        Describe("magick", "Magick.NET", typeof(MagickImage), "图片预览、图片格式转换", () => _ = MagickNET.Version),
        new("ffmpeg", "FFmpeg", EnginePackageCatalog.Ffmpeg.Version, "视频首帧、音频与视频格式转换", FfmpegReady(), Path.GetDirectoryName(paths.FfmpegExecutable)!),
        new("libreoffice", "LibreOffice 文档引擎", EnginePackageCatalog.LibreOffice.Version, "DOC/DOCX/ODT/RTF/TXT 互转与 PDF 输出（按需下载）",
            paths.OfficeExecutable is { } office && File.Exists(office), paths.OfficeExecutable ?? "设置 → 引擎管理 → 下载文档引擎"),
        Describe("pdfium", "PDFium / PDFiumCore", typeof(PDFiumCore.fpdfview), "PDF 首页预览、页面导出图片", PdfiumEngine.EnsureAvailable),
        Describe("pdfpig", "PdfPig", typeof(UglyToad.PdfPig.Writer.PdfDocumentBuilder), "图片生成 PDF")
    ];

    private bool FfmpegReady() => new[] { "ffmpeg.exe", "ffprobe.exe", "avcodec-62.dll", "avdevice-62.dll", "avfilter-11.dll", "avformat-62.dll", "avutil-60.dll", "swresample-6.dll", "swscale-9.dll" }
        .All(name => File.Exists(Path.Combine(Path.GetDirectoryName(paths.FfmpegExecutable)!, name)));
    private static ProcessingEngineInfo Describe(string id, string name, Type type, string capabilities, Action? check = null)
    {
        var available = true;
        try { check?.Invoke(); } catch (Exception) { available = false; }
        return new(id, name, type.Assembly.GetName().Version?.ToString() ?? "未知", capabilities, available, Path.GetDirectoryName(type.Assembly.Location)!);
    }

    public async Task<PreviewResult> PreviewAsync(string path, int maximumEdge, CancellationToken token)
    {
        path = LocalPath(path);
        maximumEdge = Math.Clamp(maximumEdge, 128, 2048);
        await previewGate.WaitAsync(token);
        try
        {
            return await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(path)) return new PreviewResult(PreviewKind.Unsupported, Message: "文件夹");
                var stamp = Stamp(path);
                var extension = Path.GetExtension(path);
                if (TextExtensions.Contains(extension))
                {
                    using var input = File.OpenRead(path);
                    var buffer = new byte[Math.Min(input.Length, 32_768)];
                    var count = await input.ReadAsync(buffer, token);
                    using var textStream = new MemoryStream(buffer, 0, count);
                    using var reader = new StreamReader(textStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    var text = await reader.ReadToEndAsync(token);
                    if (text.Contains('\0')) return new PreviewResult(PreviewKind.Unsupported, Message: "此文件没有可显示的文本预览。");
                    return new PreviewResult(PreviewKind.Text, Text: text, Message: input.Length > count ? "仅预览文件开头" : null);
                }
                var kind = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? PreviewKind.Pdf :
                    FfmpegEngine.Extensions.Contains(extension) ? PreviewKind.Video :
                    ConversionCapabilities.IsImage(path) ? PreviewKind.Image : PreviewKind.Unsupported;
                if (kind == PreviewKind.Unsupported) return new PreviewResult(kind, Message: "暂不支持此格式的内容预览。");
                Directory.CreateDirectory(paths.CacheDirectory);
                var key = System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{stamp.Path}|{stamp.Length}|{stamp.ModifiedUtcTicks}|{maximumEdge}|preview-v1")));
                var cache = Path.Combine(paths.CacheDirectory, key + ".png");
                if (!File.Exists(cache))
                {
                    var temporary = Path.Combine(paths.CacheDirectory, Guid.NewGuid().ToString("N") + ".png");
                    try
                    {
                        if (kind == PreviewKind.Video)
                            await new FfmpegEngine(paths.FfmpegExecutable).PreviewAsync(path, temporary, maximumEdge, token);
                        else
                        {
                            byte[] png;
                            if (kind == PreviewKind.Pdf)
                            {
                                using var image = PdfiumEngine.Render(path, 0, 150, maximumEdge, token);
                                png = image.ToByteArray(MagickFormat.Png);
                            }
                            else png = MagickImageEngine.Preview(path, maximumEdge);
                            token.ThrowIfCancellationRequested();
                            await File.WriteAllBytesAsync(temporary, png, token);
                        }
                        Verify(stamp); token.ThrowIfCancellationRequested();
                        File.Move(temporary, cache, overwrite: false);
                        TrimCache();
                    }
                    finally { DeleteTemporary(temporary); }
                }
                token.ThrowIfCancellationRequested();
                Verify(stamp);
                return new PreviewResult(kind, Png: await File.ReadAllBytesAsync(cache, token), Message: kind switch
                { PreviewKind.Video => "视频首帧", PreviewKind.Pdf => "PDF 首页", _ => null });
            }, token);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { throw Friendly(error); }
        finally { previewGate.Release(); }
    }

    public Task<ConversionPlan> CreatePlanAsync(ConversionRequest request, CancellationToken token)
        => Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested();
            var sources = new List<ProcessingSource>();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var supplied in request.Paths)
            {
                token.ThrowIfCancellationRequested();
                var path = supplied;
                try
                {
                    path = LocalPath(path);
                    if (!known.Add(path)) continue;
                    if (Directory.Exists(path)) { sources.Add(new(path, 0, 0, Error: "当前转换不接受文件夹。")); continue; }
                    var source = Stamp(path);
                    if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                        source = source with { PdfPages = PdfiumEngine.PageCount(path) };
                    else if (ConversionCapabilities.IsDocument(path))
                    {
                        if (paths.OfficeExecutable is not { } office || !File.Exists(office))
                            source = source with { Error = "文档引擎尚未安装，请前往设置 → 引擎管理下载 LibreOffice。" };
                    }
                    else if (ConversionCapabilities.IsAudio(path) || ConversionCapabilities.IsVideo(path))
                        await new FfmpegConverter(paths.FfmpegExecutable).CheckAsync(path, token);
                    else if (!ConversionCapabilities.IsImage(path))
                        source = source with { Error = "当前尚未接入此文件格式。" };
                    sources.Add(source);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { sources.Add(new(path, 0, 0, Error: Friendly(error).Message)); }
            }
            return ConversionPlanner.Create(request with { Paths = request.Paths.ToArray(), OutputDirectory = LocalPath(request.OutputDirectory) },
                sources, path => File.Exists(path) || Directory.Exists(path));
        }, token);

    public async Task<ConversionReport> ConvertAsync(ConversionPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token)
    {
        await conversionGate.WaitAsync(token);
        try { return await Task.Run(() => ConvertAsyncCore(plan, progress, token), CancellationToken.None); }
        finally { conversionGate.Release(); }
    }

    private async Task<ConversionReport> ConvertAsyncCore(ConversionPlan plan, IProgress<ProcessingProgress>? progress, CancellationToken token)
    {
        var results = new List<ConversionItemResult>();
        var directory = LocalPath(plan.Request.OutputDirectory);
        var completed = 0;
        var media = new FfmpegConverter(paths.FfmpegExecutable);
        var office = paths.OfficeExecutable;
        foreach (var item in plan.Items)
        {
            if (!item.CanRun) { results.Add(new(item, null, item.Error ?? "项目不能转换。")); continue; }
            if (token.IsCancellationRequested) { results.Add(new(item, null, "已取消，尚未处理。")); continue; }
            var target = Path.GetFullPath(item.OutputPath!);
            var temporary = Path.Combine(directory, ".fileassistant-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                if (!string.Equals(Path.GetDirectoryName(target), directory, StringComparison.OrdinalIgnoreCase) ||
                    item.Sources.Any(source => string.Equals(source.Path, target, StringComparison.OrdinalIgnoreCase)))
                    throw new FileProcessingException("输出路径不属于此转换计划，已停止此项。");
                foreach (var source in item.Sources) Verify(source);
                if (item.Sources.Count == 0 || item.Sources.Any(source => !ConversionCapabilities.GetTargets(source.Path, includeSameFormat: true).Contains(plan.Request.Target)))
                    throw new FileProcessingException("此文件不能转换为所选格式，请重新预览计划。");
                if (File.Exists(target) || Directory.Exists(target)) throw new FileProcessingException("输出位置已被其他文件占用，请重新预览计划。");
                Directory.CreateDirectory(directory);
                progress?.Report(new(completed, plan.ReadyCount, $"正在转换 {item.SourceLabel}"));
                if (ConversionCapabilities.IsDocument(item.Sources[0].Path))
                {
                    if (office is null) throw new FileProcessingException("文档引擎尚未安装，请前往设置 → 引擎管理。");
                    await new LibreOfficeEngine(office).ConvertAsync(item.Sources[0].Path, temporary, plan.Request.Target, token);
                }
                else if (ConversionCapabilities.IsAudioTarget(plan.Request.Target) || ConversionCapabilities.IsVideoTarget(plan.Request.Target))
                    await media.ConvertAsync(item.Sources[0].Path, temporary, plan.Request.Target, token);
                else using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (plan.Request.Target == ConversionTarget.Pdf) PdfPigEngine.Create(item.Sources, stream, token);
                    else
                    {
                        using var image = item.PdfPage is { } page
                            ? PdfiumEngine.Render(item.Sources[0].Path, page, plan.Request.PdfDpi, null, token)
                            : MagickImageEngine.Read(item.Sources[0].Path);
                        token.ThrowIfCancellationRequested();
                        MagickImageEngine.Write(image, stream, plan.Request.Target, plan.Request.Quality);
                    }
                    stream.Flush(flushToDisk: true);
                }
                foreach (var source in item.Sources) Verify(source);
                token.ThrowIfCancellationRequested();
                // Commit only completed output, and never overwrite a file created after preview.
                File.Move(temporary, target, overwrite: false);
                var info = new FileInfo(target);
                results.Add(new(item, new(target, info.Length, info.LastWriteTimeUtc, item.Sources.Select(source => source.Path).ToArray()), "已完成"));
            }
            catch (OperationCanceledException) { results.Add(new(item, null, "已取消此项。")); }
            catch (Exception error) { results.Add(new(item, null, Friendly(error).Message)); }
            finally { DeleteTemporary(temporary); }
            completed++;
            progress?.Report(new(completed, plan.ReadyCount, results[^1].Message));
        }
        return new(results, token.IsCancellationRequested);
    }

    private static string LocalPath(string supplied)
    {
        if (!Path.IsPathFullyQualified(supplied) || supplied.Contains("://", StringComparison.Ordinal) || supplied.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new FileProcessingException("请选择实际文件或完整的输出文件夹路径。");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(supplied));
    }
    private static ProcessingSource Stamp(string path)
    {
        var file = new FileInfo(path);
        return new(path, file.Length, file.LastWriteTimeUtc.Ticks);
    }
    private static void Verify(ProcessingSource source)
    {
        var actual = Stamp(source.Path);
        if (actual.Length != source.Length || actual.ModifiedUtcTicks != source.ModifiedUtcTicks)
            throw new FileProcessingException("输入文件已变化，请重新预览转换计划。");
    }
    private static FileProcessingException Friendly(Exception error) => error switch
    {
        FileProcessingException processing => processing,
        FileNotFoundException or DirectoryNotFoundException => new("文件或目录已移动、删除，请重新添加。", error),
        UnauthorizedAccessException => new("没有访问此文件或输出位置的权限。", error),
        DllNotFoundException or BadImageFormatException => new("本地引擎组件不可用，请检查安装或引擎管理。", error),
        IOException => new("无法读写文件，请检查占用、磁盘空间或输出位置后重试。", error),
        _ => new("无法处理此文件，文件可能损坏或格式不支持。", error)
    };
    private static void DeleteTemporary(string path)
    { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private void TrimCache()
    {
        var files = new DirectoryInfo(paths.CacheDirectory).EnumerateFiles("*.png").OrderByDescending(file => file.LastWriteTimeUtc).Skip(200);
        foreach (var file in files) DeleteTemporary(file.FullName);
    }
}
