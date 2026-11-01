using System.Security.Cryptography;
using System.Text;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;
using ImageMagick;
using ImageMagick.Drawing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FileAssistant.Infrastructure.Tests;

public sealed class NextStageConversionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-next-test-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileProcessingService service;
    public NextStageConversionTests() { Directory.CreateDirectory(root); service = new(new("unused", Path.Combine(root, "cache"))); }
    private async Task<ConversionReport> Convert(string source, ConversionTarget target, ConversionSettings? settings = null, int quality = 90)
    {
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "out"), target, quality, Settings: settings), default);
        Assert.True(plan.ReadyCount > 0, string.Join(";", plan.Items.Select(item => item.Description)));
        return await service.ConvertAsync(plan, null, default);
    }
    private static string Output(ConversionReport report)
    {
        Assert.True(report.Outputs.Count == 1, string.Join(";", report.Items.Select(item => item.Message)));
        return report.Outputs[0].FullPath;
    }

    [Theory]
    [InlineData(ConversionTarget.Png)]
    [InlineData(ConversionTarget.Jpeg)]
    [InlineData(ConversionTarget.WebP)]
    public async Task Svg_rasterizes_content_and_keeps_transparency_where_supported(ConversionTarget target)
    {
        var source = Path.Combine(root, "vector.svg");
        await File.WriteAllTextAsync(source, """<svg xmlns="http://www.w3.org/2000/svg" width="80" height="60"><rect x="20" y="20" width="30" height="30" fill="red"/></svg>""");
        using var image = new MagickImage(Output(await Convert(source, target)));
        Assert.Equal(80u, image.Width); Assert.Equal(60u, image.Height);
        using var pixels = image.GetPixels();
        var red = pixels.GetPixel(30, 30).ToColor()!; Assert.True(red.R > 60000 && red.B < 5000);
        var corner = pixels.GetPixel(0, 0).ToColor()!;
        if (target == ConversionTarget.Jpeg) Assert.True(corner.R > 60000 && corner.G > 60000);
        else Assert.Equal(0, corner.A);
    }

    [Fact]
    public async Task Svg_external_resources_are_rejected_without_fetching_them()
    {
        var source = Path.Combine(root, "external.svg");
        await File.WriteAllTextAsync(source, """<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"><image href="https://example.invalid/private.png"/></svg>""");
        var plan = await service.CreatePlanAsync(new([source], root, ConversionTarget.Png), default);
        Assert.Equal(0, plan.ReadyCount); Assert.Contains("外部资源", plan.Items[0].Error);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    public async Task Icon_contains_seven_sizes_with_valid_pixels(string extension)
    {
        var source = Path.Combine(root, "icon." + extension);
        using (var image = new MagickImage(MagickColors.Red, 96, 48)) image.Write(source);
        using var icons = new MagickImageCollection(Output(await Convert(source, ConversionTarget.Ico)));
        Assert.Equal(new uint[] { 16, 24, 32, 48, 64, 128, 256 }, icons.Select(icon => icon.Width));
        Assert.All(icons, icon => Assert.Equal(icon.Width, icon.Height));
    }

    [Fact]
    public async Task Animation_roundtrip_keeps_frames_delays_loop_and_transparency()
    {
        var source = Path.Combine(root, "animation.gif");
        using (var frames = new MagickImageCollection())
        {
            foreach (var (color, delay) in new[] { (MagickColors.Red, 12u), (MagickColors.Blue, 31u), (MagickColors.Green, 20u) })
            {
                var frame = new MagickImage(MagickColors.Transparent, 48, 40) { AnimationDelay = delay, AnimationIterations = 3, GifDisposeMethod = GifDisposeMethod.Background };
                new Drawables().FillColor(color).Rectangle(12, 12, 24, 24).Draw(frame); frames.Add(frame);
            }
            frames.Write(source);
        }
        var webp = Output(await Convert(source, ConversionTarget.WebP));
        var gif = Output(await Convert(webp, ConversionTarget.Gif));
        foreach (var path in new[] { source, webp, gif })
        {
            using var frames = new MagickImageCollection(path);
            frames.Coalesce();
            Assert.Equal(3, frames.Count); Assert.Equal(3u, frames[0].AnimationIterations);
            Assert.Equal(new[] { .12, .31, .20 }, frames.Select(frame => frame.AnimationDelay / (double)frame.AnimationTicksPerSecond));
            using var pixels = frames[0].GetPixels(); Assert.True(pixels.GetPixel(0, 0).ToColor()!.A == 0, path);
            Assert.True(pixels.GetPixel(15, 15).ToColor()!.R > 60000);
            using var blue = frames[1].GetPixels(); Assert.True(blue.GetPixel(15, 15).ToColor()!.B > 60000);
        }
        using var staticImage = new MagickImage(Output(await Convert(source, ConversionTarget.Png, new(Frame: 2))));
        using var staticPixels = staticImage.GetPixels(); Assert.True(staticPixels.GetPixel(15, 15).ToColor()!.B > 60000);
    }

    [Fact]
    public async Task Tiff_exports_all_pages_or_explicit_page_and_makes_multipage_pdf()
    {
        var source = Path.Combine(root, "pages.tiff");
        using (var images = new MagickImageCollection())
        { images.Add(new MagickImage(MagickColors.Red, 40, 60)); images.Add(new MagickImage(MagickColors.Blue, 60, 40)); images.Write(source); }
        using (var original = new MagickImageCollection(source)) Assert.Equal(2, original.Count);
        var pages = await Convert(source, ConversionTarget.Png);
        Assert.All(pages.Items, item => Assert.True(item.Output is not null, item.Message));
        Assert.Equal(2, pages.Outputs.Count); Assert.Contains("001", pages.Outputs[0].FullPath); Assert.Contains("002", pages.Outputs[1].FullPath);
        using var blue = new MagickImage(Output(await Convert(source, ConversionTarget.Png, new(Frame: 2, AllTiffPages: false))));
        using var pixels = blue.GetPixels(); Assert.Equal(65535, pixels.GetPixel(0, 0).ToColor()!.B);
        using var pdf = PdfDocument.Open(Output(await Convert(source, ConversionTarget.Pdf))); Assert.Equal(2, pdf.NumberOfPages);
        var bad = await service.CreatePlanAsync(new([source], root, ConversionTarget.Png, Settings: new(Frame: 3, AllTiffPages: false)), default);
        Assert.Equal(0, bad.ReadyCount);
    }

    [Fact]
    public async Task Animation_keeps_identical_frames_infinite_loop_and_cleared_canvas()
    {
        var source = Path.Combine(root, "duplicates.gif");
        using (var frames = new MagickImageCollection())
        {
            foreach (var color in new[] { MagickColors.Red, MagickColors.Red, MagickColors.Transparent })
                frames.Add(new MagickImage(color, 32, 32) { AnimationDelay = 10, AnimationIterations = 0, GifDisposeMethod = GifDisposeMethod.Background });
            frames.Write(source);
        }
        var webp = Output(await Convert(source, ConversionTarget.WebP));
        var gif = Output(await Convert(webp, ConversionTarget.Gif));
        foreach (var path in new[] { webp, gif })
        {
            using var frames = new MagickImageCollection(path); frames.Coalesce();
            Assert.Equal(3, frames.Count); Assert.Equal(0u, frames[0].AnimationIterations);
            Assert.All(frames, frame => Assert.Equal(.1, frame.AnimationDelay / (double)frame.AnimationTicksPerSecond));
            using var pixels = frames[2].GetPixels(); Assert.True(pixels.GetPixel(5, 5).ToColor()!.A == 0, path);
        }
    }

    [Fact]
    public async Task Same_jpeg_processing_resizes_numbers_collisions_and_preserves_input()
    {
        var source = Path.Combine(root, "photo.jpg"); using (var image = new MagickImage(MagickColors.Red, 300, 200)) image.Write(source);
        var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var request = new ConversionRequest([source], root, ConversionTarget.Jpeg, 65, Settings: new(Mode: ConversionMode.Process, MaximumEdge: 90));
        var first = Output(await service.ConvertAsync(await service.CreatePlanAsync(request, default), null, default));
        var second = Output(await service.ConvertAsync(await service.CreatePlanAsync(request, default), null, default));
        Assert.NotEqual(first, second); Assert.NotEqual(source, first);
        using var result = new MagickImage(first); Assert.Equal(90u, result.Width); Assert.Equal(60u, result.Height);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    [Fact]
    public async Task Pdf_text_recovers_page_order_lines_and_rejects_image_only_documents()
    {
        var source = Path.Combine(root, "text.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(400, 500);
            page.AddText("Second line", 12, new PdfPoint(20, 400), font);
            page.AddText("First line", 12, new PdfPoint(20, 440), font);
            builder.AddPage(400, 500).AddText("Next page", 12, new PdfPoint(20, 440), font);
            await File.WriteAllBytesAsync(source, builder.Build());
        }
        var output = Output(await Convert(source, ConversionTarget.Txt));
        var text = await File.ReadAllTextAsync(output, new UTF8Encoding(false, true));
        Assert.True(text.IndexOf("First line", StringComparison.Ordinal) < text.IndexOf("Second line", StringComparison.Ordinal));
        Assert.Contains("\n", text); Assert.Contains('\f', text); Assert.EndsWith("Next page", text);
        var image = Path.Combine(root, "scan.png"); using (var red = new MagickImage(MagickColors.Red, 40, 40)) red.Write(image);
        var scan = Output(await Convert(image, ConversionTarget.Pdf));
        var rejected = await Convert(scan, ConversionTarget.Txt); Assert.Empty(rejected.Outputs); Assert.Contains("OCR", rejected.Items[0].Message);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "out"), "*.tmp"));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
