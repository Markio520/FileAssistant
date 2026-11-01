namespace FileAssistant.Core.Engines;

public enum ConversionTarget { Png, Jpeg, WebP, Bmp, Pdf, Tiff, Gif, Avif, Doc, Docx, Odt, Rtf, Txt, Mp3, Wav, Flac, M4a, Mp4, Mkv, Mov, WebM, Aac, Ogg, Xls, Xlsx, Ods, Csv, Tsv, Ppt, Pptx, Odp, Ico }
public enum PreviewKind { Image, Video, Pdf, Text, Unsupported }
public sealed record PreviewResult(PreviewKind Kind, byte[]? Png = null, string? Text = null, string? Message = null);
public sealed record ProcessingEngineInfo(string Id, string Name, string Version, string Capabilities, bool IsAvailable, string Location)
{
    public string StatusLabel => IsAvailable ? "已就绪" : Id == "libreoffice" ? "未安装 · 按需下载" : "组件缺失";
}
public sealed record ConversionRequest(IReadOnlyList<string> Paths, string OutputDirectory, ConversionTarget Target, int Quality = 90, int PdfDpi = 150, ConversionSettings? Settings = null)
{
    public ConversionSettings Options => Settings ?? new();
}
public sealed record ProcessingSource(string Path, long Length, long ModifiedUtcTicks, int PdfPages = 0, string? Error = null,
    int Frames = 1, IReadOnlyList<string>? Sheets = null, IReadOnlyList<string>? AudioTracks = null);
public sealed record ConversionPlanItem(IReadOnlyList<ProcessingSource> Sources, string? OutputPath, int? PdfPage = null, string? Error = null,
    int? ImageFrame = null, int? Sheet = null, string? Detail = null)
{
    public bool CanRun => OutputPath is not null && Error is null;
    public string SourceLabel => Sources.Count == 1 ? System.IO.Path.GetFileName(Sources[0].Path) : $"{Sources.Count} 张图片";
    public string Description => Error ?? $"{SourceLabel}{(PdfPage is { } page ? $" · 第 {page + 1} 页" : "")}{(Detail is null ? "" : " · " + Detail)} → {System.IO.Path.GetFileName(OutputPath)}";
}
public sealed record ConversionPlan(ConversionRequest Request, IReadOnlyList<ConversionPlanItem> Items)
{
    public int ReadyCount => Items.Count(item => item.CanRun);
    public int RejectedCount => Items.Count(item => !item.CanRun);
}
public sealed record ProducedFile(string FullPath, long Size, DateTimeOffset Modified, IReadOnlyList<string> SourcePaths);
public sealed record ConversionItemResult(ConversionPlanItem Item, ProducedFile? Output, string Message);
public sealed record ProcessingProgress(int Completed, int Total, string Message);
public sealed record ConversionReport(IReadOnlyList<ConversionItemResult> Items, bool Cancelled)
{
    public IReadOnlyList<ProducedFile> Outputs => Items.Where(item => item.Output is not null).Select(item => item.Output!).ToArray();
    public int Failed => Items.Count(item => item.Output is null);
    public string Summary => $"成功 {Outputs.Count} 项 · 未完成 {Failed} 项{(Cancelled ? " · 已取消" : string.Empty)}";
}
public sealed class FileProcessingException(string message, Exception? inner = null) : Exception(message, inner);
