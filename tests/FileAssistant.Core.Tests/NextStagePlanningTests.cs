using FileAssistant.Core.Engines;

namespace FileAssistant.Core.Tests;

public sealed class NextStagePlanningTests
{
    [Fact]
    public void Spreadsheet_presentation_and_image_batches_stay_separate()
    {
        Assert.Contains(ConversionTarget.Xls, ConversionCapabilities.GetCommonTargets(["a.xlsx", "b.ods"]));
        Assert.Contains(ConversionTarget.Ppt, ConversionCapabilities.GetCommonTargets(["a.pptx", "b.odp"]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["a.xlsx", "b.pptx"]));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["a.ods", "b.png"]));
        Assert.Equal([ConversionTarget.Png, ConversionTarget.Jpeg, ConversionTarget.WebP], ConversionCapabilities.GetTargets("a.svg"));
    }
    [Fact]
    public void Process_mode_only_offers_actual_same_format_processing()
    {
        Assert.Equal([ConversionTarget.Jpeg], ConversionCapabilities.GetCommonTargets(["a.jpg", "b.jpeg"], ConversionMode.Process));
        Assert.Equal([ConversionTarget.Mp4], ConversionCapabilities.GetCommonTargets(["a.mp4"], ConversionMode.Process));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["a.pdf"], ConversionMode.Process));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["a.docx"], ConversionMode.Process));
        Assert.Empty(ConversionCapabilities.GetCommonTargets(["a.jpg", "b.png"], ConversionMode.Process));
    }
    [Fact]
    public void Pdf_text_is_one_output_and_csv_names_are_sanitized_and_reserved()
    {
        var request = new ConversionRequest([@"C:\a.pdf"], @"C:\out", ConversionTarget.Txt);
        Assert.Equal(1, ConversionPlanner.Create(request, [new(request.Paths[0], 1, 1, PdfPages: 4)], _ => false).ReadyCount);
        request = request with { Paths = [@"C:\a.xlsx"], Target = ConversionTarget.Csv };
        var plan = ConversionPlanner.Create(request, [new(request.Paths[0], 1, 1, Sheets: ["a/b", "a:b"])], _ => false);
        Assert.Equal(2, plan.ReadyCount); Assert.NotEqual(plan.Items[0].OutputPath, plan.Items[1].OutputPath);
        Assert.All(plan.Items, item => Assert.Equal(@"C:\out", Path.GetDirectoryName(item.OutputPath)));
        Assert.Equal(1, plan.Items[0].Sheet); Assert.Equal(2, plan.Items[1].Sheet);
    }
    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1280, 12345, 1)]
    [InlineData(1280, 44100, 0)]
    public void Invalid_engine_parameters_fail_before_planning(int size, int sampleRate, int track)
        => Assert.Throws<FileProcessingException>(() => new ConversionSettings(MaximumEdge: size, SampleRate: sampleRate, AudioTrack: track).Validate());
}
