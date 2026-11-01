using FileAssistant.Core.Engines;
using ImageMagick;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace FileAssistant.Infrastructure.Engines.Images;

internal static class MagickImageEngine
{
    private static MagickFormat Format(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => MagickFormat.Jpeg, ".png" => MagickFormat.Png,
        ".webp" => MagickFormat.WebP, ".bmp" => MagickFormat.Bmp, ".gif" => MagickFormat.Gif,
        ".tif" or ".tiff" => MagickFormat.Tiff, ".ico" => MagickFormat.Ico,
        ".avif" => MagickFormat.Avif, ".heic" or ".heif" => MagickFormat.Heic, ".svg" => MagickFormat.Svg,
        _ => throw new FileProcessingException("此图片格式尚未接入。")
    };

    internal static MagickImage Read(string path, int frame = 0)
    {
        ValidateSvg(path);
        using var input = File.OpenRead(path);
        var format = Format(path);
        var settings = new MagickReadSettings { FrameIndex = (uint)frame, FrameCount = 1, Format = format, BackgroundColor = MagickColors.Transparent };
        // Ping only the first image: some TIFF decoders cannot ping a nonzero scene through a stream.
        var info = new MagickImageInfo(input, new MagickReadSettings { Format = format, FrameCount = 1 });
        if (info.Width == 0 || info.Height == 0 || (long)info.Width * info.Height > 100_000_000)
            throw new FileProcessingException("图片尺寸过大或无效，当前支持一亿像素以内的图片。");
        input.Position = 0;
        var image = frame == 0 ? new MagickImage(input, settings) : ReadSelectedFrame(path, frame, format);
        try
        {
            image.AutoOrient();
            image.ColorSpace = ColorSpace.sRGB;
            return image;
        }
        catch { image.Dispose(); throw; }
    }

    private static MagickImage ReadSelectedFrame(string path, int frame, MagickFormat format)
    {
        if (format is not (MagickFormat.Gif or MagickFormat.WebP))
            return new MagickImage(path, new MagickReadSettings { Format = format, FrameIndex = (uint)frame, FrameCount = 1, BackgroundColor = MagickColors.Transparent });
        using var frames = new MagickImageCollection(path, new MagickReadSettings { Format = format, BackgroundColor = MagickColors.Transparent });
        if (frame >= frames.Count) throw new FileProcessingException("指定帧/页不存在。");
        frames.Coalesce();
        return (MagickImage)frames[frame].Clone();
    }

    internal static int FrameCount(string path)
    {
        if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)) { using var image = Read(path); return 1; }
        using var images = new MagickImageCollection();
        images.Ping(path, new MagickReadSettings { Format = Format(path) });
        if (images.Count == 0 || images.Count > 10000 || (long)images.Max(image => image.Width) * images.Max(image => image.Height) * images.Count > 100_000_000)
            throw new FileProcessingException("图片帧数或尺寸超出限制（10000 帧，展开画布合计一亿像素）。");
        return images.Count;
    }

    // SVG stays self-contained: do not resolve network/file resources or XML entities.
    private static void ValidateSvg(string path)
    {
        if (!Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)) return;
        using var reader = System.Xml.XmlReader.Create(path, new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
        var xml = XDocument.Load(reader);
        var values = xml.Descendants().Attributes().Select(attribute => attribute.Value)
            .Concat(xml.Descendants().Where(element => element.Name.LocalName == "style").Select(element => element.Value)).ToArray();
        if (xml.DescendantNodes().OfType<XProcessingInstruction>().Any() || xml.Descendants().Any(element => element.Name.LocalName == "script") ||
            xml.Descendants().Attributes().Any(attribute => attribute.Name.LocalName is "href" &&
            !attribute.Value.Trim().StartsWith('#') && !attribute.Value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) &&
            !attribute.Value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)) ||
            values.Any(value => value.Contains("@import", StringComparison.OrdinalIgnoreCase) ||
                Regex.Matches(value, "url\\(([^)]*)\\)", RegexOptions.IgnoreCase).Any(match => !match.Groups[1].Value.Trim().Trim('\'', '"').StartsWith('#'))))
            throw new FileProcessingException("SVG 含外部资源；请先将资源嵌入文件后再转换（仅本地处理）。");
    }

    internal static void Convert(string path, Stream output, ConversionRequest request, int frame, CancellationToken token)
    {
        if (ConversionCapabilities.IsAnimation(path, request.Target))
        {
            using var images = new MagickImageCollection(path, new MagickReadSettings { Format = Format(path), BackgroundColor = MagickColors.Transparent });
            foreach (var image in images) image.BackgroundColor = MagickColors.Transparent;
            // Coalesce resolves partial frames and disposal before encoding another animation format.
            images.Coalesce();
            foreach (var image in images)
            {
                token.ThrowIfCancellationRequested();
                Resize(image, request.Options.MaximumEdge);
                image.Quality = (uint)request.Quality;
                image.ColorType = ColorType.TrueColorAlpha;
                image.ColorSpace = ColorSpace.sRGB;
                image.BackgroundColor = MagickColors.Transparent;
                image.GifDisposeMethod = GifDisposeMethod.Background;
                if (request.Target == ConversionTarget.WebP) image.Settings.SetDefine(MagickFormat.WebP, "lossless", true);
            }
            if (request.Target == ConversionTarget.WebP && images.Count > 1) WebpAnimationWriter.Write(images, output, token);
            else images.Write(output, request.Target == ConversionTarget.Gif ? MagickFormat.Gif : MagickFormat.WebP);
            token.ThrowIfCancellationRequested();
            return;
        }
        using var single = Read(path, frame);
        if (request.Target == ConversionTarget.Ico)
        {
            using var icons = new MagickImageCollection();
            foreach (var size in new uint[] { 16, 24, 32, 48, 64, 128, 256 })
            {
                token.ThrowIfCancellationRequested();
                var icon = single.Clone(); icon.Resize(size, size);
                icon.BackgroundColor = MagickColors.Transparent; icon.Extent(size, size, Gravity.Center);
                icons.Add(icon);
            }
            icons.Write(output, MagickFormat.Ico);
        }
        else { Resize(single, request.Options.MaximumEdge); Write(single, output, request.Target, request.Quality); }
    }

    internal static void Resize(IMagickImage<ushort> image, int edge)
    {
        if (edge > 0) image.Resize(new MagickGeometry((uint)edge, (uint)edge) { Greater = true });
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
            ConversionTarget.Png => MagickFormat.Png,
            ConversionTarget.Tiff => MagickFormat.Tiff,
            ConversionTarget.Gif => MagickFormat.Gif,
            ConversionTarget.Avif => MagickFormat.Avif,
            _ => throw new FileProcessingException("此目标不是图片格式。")
        });
    }
}
