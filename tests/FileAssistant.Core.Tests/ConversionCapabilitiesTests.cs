using FileAssistant.Core.Engines;

namespace FileAssistant.Core.Tests;

public sealed class ConversionCapabilitiesTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    public void Jpeg_sources_offer_other_image_formats_and_pdf(string path)
    {
        Assert.Equal([ConversionTarget.Png, ConversionTarget.WebP, ConversionTarget.Bmp, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif, ConversionTarget.Pdf],
            ConversionCapabilities.GetTargets(path));
    }

    [Fact]
    public void Mixed_png_and_webp_only_offer_common_destinations()
    {
        Assert.Equal([ConversionTarget.Jpeg, ConversionTarget.Bmp, ConversionTarget.Pdf, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif],
            ConversionCapabilities.GetCommonTargets(["one.PNG", "two.webp"]));
    }

    [Theory]
    [InlineData("archive.zip")]
    [InlineData("program.exe")]
    [InlineData("unknown.xyz")]
    public void Unsupported_sources_block_a_common_target_instead_of_being_silently_ignored(string path)
    {
        Assert.Empty(ConversionCapabilities.GetTargets(path));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["photo.png", path]));
    }

    [Fact]
    public void Pdf_exports_pages_to_images_and_can_share_valid_destinations_with_images()
    {
        Assert.Equal([ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Bmp, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif],
            ConversionCapabilities.GetTargets("document.PDF"));
        Assert.Equal([ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Bmp, ConversionTarget.Tiff, ConversionTarget.Gif, ConversionTarget.Avif],
            ConversionCapabilities.GetCommonTargets(["document.pdf", "image.png"]));
    }

    [Fact]
    public void Empty_input_and_batches_without_a_common_output_offer_no_targets()
    {
        Assert.Empty(ConversionCapabilities.GetCommonTargets([]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["image.png", "photo.jpg", "image.webp", "image.bmp", "image.tif", "image.gif", "image.avif", "document.pdf"]));
    }

    [Fact]
    public void Documents_have_common_text_targets_but_cannot_be_batched_with_images()
    {
        Assert.Equal([ConversionTarget.Pdf, ConversionTarget.Doc, ConversionTarget.Odt, ConversionTarget.Rtf],
            ConversionCapabilities.GetCommonTargets(["one.DOCX", "two.txt"]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["one.docx", "photo.png"]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["one.docx", "document.pdf"]));
        Assert.DoesNotContain(ConversionTarget.Tiff, ConversionCapabilities.GetTargets("photo.TIF"));
    }

    [Fact]
    public void Audio_and_video_menus_only_contain_compatible_targets()
    {
        Assert.Equal([ConversionTarget.Flac, ConversionTarget.M4a, ConversionTarget.Aac, ConversionTarget.Ogg], ConversionCapabilities.GetCommonTargets(["one.MP3", "two.wav"]));
        Assert.Equal([ConversionTarget.Mkv, ConversionTarget.WebM], ConversionCapabilities.GetCommonTargets(["one.mp4", "two.mov"]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["one.wav", "two.mp4"]));
        Assert.DoesNotContain(ConversionTarget.Mp4, ConversionCapabilities.GetTargets("one.M4V"));
    }

    [Fact]
    public void Document_pdf_outputs_stay_individual_and_bad_routes_are_rejected()
    {
        var request = new ConversionRequest([@"C:\one.docx", @"C:\two.txt"], @"C:\out", ConversionTarget.Pdf);
        var plan = ConversionPlanner.Create(request, [new(request.Paths[0], 1, 1), new(request.Paths[1], 1, 1)], _ => false);
        Assert.Equal(2, plan.ReadyCount);
        Assert.All(plan.Items, item => Assert.Single(item.Sources));
        Assert.EndsWith("one.pdf", plan.Items[0].OutputPath);
        var invalid = ConversionPlanner.Create(request with { Target = ConversionTarget.Mp4 }, [new(request.Paths[0], 1, 1)], _ => false);
        Assert.Equal(0, invalid.ReadyCount);
        Assert.Equal(1, invalid.RejectedCount);
    }
}
