namespace FileAssistant.Core.Engines;

/// <summary>Implemented conversion routes, shared by input validation and target selection.</summary>
public static class ConversionCapabilities
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".avif", ".heic", ".heif", ".svg" };
    private static readonly ConversionTarget[] ImageTargets =
        [ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Bmp, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif];
    private static readonly ConversionTarget[] DocumentTargets =
        [ConversionTarget.Doc, ConversionTarget.Docx, ConversionTarget.Odt, ConversionTarget.Rtf, ConversionTarget.Txt, ConversionTarget.Pdf];
    private static readonly ConversionTarget[] AudioTargets = [ConversionTarget.Mp3, ConversionTarget.Wav, ConversionTarget.Flac, ConversionTarget.M4a, ConversionTarget.Aac, ConversionTarget.Ogg];
    private static readonly ConversionTarget[] VideoTargets = [ConversionTarget.Mp4, ConversionTarget.Mkv, ConversionTarget.Mov, ConversionTarget.WebM];
    private static readonly ConversionTarget[] SpreadsheetTargets = [ConversionTarget.Xls, ConversionTarget.Xlsx, ConversionTarget.Ods, ConversionTarget.Csv, ConversionTarget.Tsv, ConversionTarget.Pdf];
    private static readonly ConversionTarget[] PresentationTargets = [ConversionTarget.Ppt, ConversionTarget.Pptx, ConversionTarget.Odp, ConversionTarget.Pdf];

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));
    public static bool IsDocument(string path) => new[] { ".doc", ".docx", ".odt", ".rtf", ".txt" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsSpreadsheet(string path) => new[] { ".xls", ".xlsx", ".ods", ".csv", ".tsv" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsPresentation(string path) => new[] { ".ppt", ".pptx", ".odp" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsOffice(string path) => IsDocument(path) || IsSpreadsheet(path) || IsPresentation(path);
    public static bool IsTiff(string path) => Path.GetExtension(path).ToLowerInvariant() is ".tif" or ".tiff";
    public static bool IsAnimation(string path, ConversionTarget target) => Path.GetExtension(path).ToLowerInvariant() is ".gif" or ".webp" && target is ConversionTarget.Gif or ConversionTarget.WebP;
    public static bool IsAudio(string path) => new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsVideo(string path) => new[] { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".wmv", ".m4v" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsImageTarget(ConversionTarget target) => ImageTargets.Contains(target);
    public static bool IsAudioTarget(ConversionTarget target) => AudioTargets.Contains(target);
    public static bool IsVideoTarget(ConversionTarget target) => VideoTargets.Contains(target);
    public static string Extension(ConversionTarget target) => target switch
    {
        ConversionTarget.Jpeg => ".jpg", ConversionTarget.Tiff => ".tiff", _ => "." + target.ToString().ToLowerInvariant()
    };

    public static IReadOnlyList<ConversionTarget> GetTargets(string path, bool includeSameFormat = false)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        ConversionTarget? original = extension switch
        {
            ".png" => ConversionTarget.Png, ".jpg" or ".jpeg" => ConversionTarget.Jpeg,
            ".webp" => ConversionTarget.WebP, ".bmp" => ConversionTarget.Bmp, ".pdf" => ConversionTarget.Pdf,
            ".tif" or ".tiff" => ConversionTarget.Tiff, ".gif" => ConversionTarget.Gif, ".avif" => ConversionTarget.Avif,
            ".doc" => ConversionTarget.Doc, ".docx" => ConversionTarget.Docx, ".odt" => ConversionTarget.Odt,
            ".rtf" => ConversionTarget.Rtf, ".txt" => ConversionTarget.Txt,
            ".mp3" => ConversionTarget.Mp3, ".wav" => ConversionTarget.Wav, ".flac" => ConversionTarget.Flac, ".m4a" => ConversionTarget.M4a,
            ".aac" => ConversionTarget.Aac, ".ogg" => ConversionTarget.Ogg,
            ".mp4" or ".m4v" => ConversionTarget.Mp4, ".mkv" => ConversionTarget.Mkv, ".mov" => ConversionTarget.Mov, ".webm" => ConversionTarget.WebM,
            _ => null
        };
        original ??= Enum.TryParse<ConversionTarget>(extension.TrimStart('.'), true, out var parsed) ? parsed : null;
        if (extension == ".pdf") return [.. ImageTargets, ConversionTarget.Txt];
        IReadOnlyList<ConversionTarget> targets = extension == ".svg" ? [ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP]
            : IsImage(path) ? [.. ImageTargets, ConversionTarget.Pdf, .. extension is ".png" or ".jpg" or ".jpeg" ? new[] { ConversionTarget.Ico } : Array.Empty<ConversionTarget>()]
            : IsDocument(path) ? DocumentTargets : IsSpreadsheet(path) ? SpreadsheetTargets : IsPresentation(path) ? PresentationTargets
            : IsAudio(path) ? AudioTargets : IsVideo(path) ? [.. VideoTargets, ConversionTarget.Mp3, ConversionTarget.Wav, ConversionTarget.M4a, ConversionTarget.Flac] : [];
        return [.. targets.Where(target => includeSameFormat || target != original)];
    }

    public static IReadOnlyList<ConversionTarget> GetProcessingTargets(string path)
    {
        if (!(IsImage(path) || IsAudio(path) || IsVideo(path))) return [];
        var same = GetTargets(path, true).Except(GetTargets(path)).ToArray();
        return same.Where(target => IsImageTarget(target) || IsAudioTarget(target) || IsVideoTarget(target)).ToArray();
    }

    public static IReadOnlyList<ConversionTarget> GetCommonTargets(IEnumerable<string> paths, ConversionMode mode = ConversionMode.Convert)
    {
        HashSet<ConversionTarget>? common = null;
        string? category = null;
        foreach (var path in paths)
        {
            // Document batches are kept separate from images/PDF, even when both can produce PDF.
            var kind = IsDocument(path) ? "document" : IsSpreadsheet(path) ? "spreadsheet" : IsPresentation(path) ? "presentation"
                : IsAudio(path) ? "audio" : IsVideo(path) ? "video" : "image/pdf";
            if (category is { } previous && previous != kind) return [];
            category = kind;
            var targets = mode == ConversionMode.Process ? GetProcessingTargets(path) : GetTargets(path);
            if (common is null) common = [.. targets];
            else common.IntersectWith(targets);
            if (common.Count == 0) return [];
        }
        return common is null ? [] : [.. common.OrderBy(target => target)];
    }
}
