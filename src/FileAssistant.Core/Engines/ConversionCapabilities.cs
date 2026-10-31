namespace FileAssistant.Core.Engines;

/// <summary>Implemented conversion routes, shared by input validation and target selection.</summary>
public static class ConversionCapabilities
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".avif", ".heic", ".heif" };
    private static readonly ConversionTarget[] ImageTargets =
        [ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Bmp, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif];
    private static readonly ConversionTarget[] DocumentTargets =
        [ConversionTarget.Doc, ConversionTarget.Docx, ConversionTarget.Odt, ConversionTarget.Rtf, ConversionTarget.Txt, ConversionTarget.Pdf];
    private static readonly ConversionTarget[] AudioTargets = [ConversionTarget.Mp3, ConversionTarget.Wav, ConversionTarget.Flac, ConversionTarget.M4a, ConversionTarget.Aac, ConversionTarget.Ogg];
    private static readonly ConversionTarget[] VideoTargets = [ConversionTarget.Mp4, ConversionTarget.Mkv, ConversionTarget.Mov, ConversionTarget.WebM];

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));
    public static bool IsDocument(string path) => new[] { ".doc", ".docx", ".odt", ".rtf", ".txt" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
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
        if (extension == ".pdf") return [.. ImageTargets];
        IReadOnlyList<ConversionTarget> targets = IsImage(path) ? [.. ImageTargets, ConversionTarget.Pdf]
            : IsDocument(path) ? DocumentTargets : IsAudio(path) ? AudioTargets : IsVideo(path) ? VideoTargets : [];
        return [.. targets.Where(target => includeSameFormat || target != original)];
    }

    public static IReadOnlyList<ConversionTarget> GetCommonTargets(IEnumerable<string> paths)
    {
        HashSet<ConversionTarget>? common = null;
        bool? documents = null;
        foreach (var path in paths)
        {
            // Document batches are kept separate from images/PDF, even when both can produce PDF.
            var document = IsDocument(path);
            if (documents is { } previous && previous != document) return [];
            documents = document;
            var targets = GetTargets(path);
            if (common is null) common = [.. targets];
            else common.IntersectWith(targets);
            if (common.Count == 0) return [];
        }
        return common is null ? [] : [.. common.OrderBy(target => target)];
    }
}
