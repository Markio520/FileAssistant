using FileAssistant.Core.Engines;
using ImageMagick;

namespace FileAssistant.Infrastructure.Engines.Images;

internal static class MagickImageEngine
{
    internal static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".avif", ".heic", ".heif" };

    internal static MagickImage Read(string path)
    {
        using var input = File.OpenRead(path);
        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => MagickFormat.Jpeg, ".png" => MagickFormat.Png,
            ".webp" => MagickFormat.WebP, ".bmp" => MagickFormat.Bmp, ".gif" => MagickFormat.Gif,
            ".tif" or ".tiff" => MagickFormat.Tiff, ".ico" => MagickFormat.Ico,
            ".avif" => MagickFormat.Avif, ".heic" or ".heif" => MagickFormat.Heic,
            _ => throw new FileProcessingException("此图片格式尚未接入。")
        };
        var settings = new MagickReadSettings { FrameCount = 1, Format = format };
        var info = new MagickImageInfo(input, settings);
        if (info.Width == 0 || info.Height == 0 || (long)info.Width * info.Height > 100_000_000)
            throw new FileProcessingException("图片尺寸过大或无效，当前支持一亿像素以内的图片。");
        input.Position = 0;
        var image = new MagickImage(input, settings);
        try
        {
            image.AutoOrient();
            image.ColorSpace = ColorSpace.sRGB;
            return image;
        }
        catch { image.Dispose(); throw; }
    }

    internal static byte[] Preview(string path, int maximumEdge)
    {
        using var image = Read(path);
        image.Resize(new MagickGeometry((uint)maximumEdge, (uint)maximumEdge) { Greater = true });
        return image.ToByteArray(MagickFormat.Png);
    }

    internal static void Write(MagickImage image, Stream output, ConversionTarget target, int quality)
    {
        image.Quality = (uint)quality;
        if (target is ConversionTarget.Jpeg or ConversionTarget.Bmp)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaAction.Remove);
        }
        image.Write(output, target switch
        {
            ConversionTarget.Jpeg => MagickFormat.Jpeg,
            ConversionTarget.WebP => MagickFormat.WebP,
            ConversionTarget.Bmp => MagickFormat.Bmp,
            _ => MagickFormat.Png
        });
    }
}
