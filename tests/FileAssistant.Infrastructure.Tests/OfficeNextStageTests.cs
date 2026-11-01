using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;
using ImageMagick;
using UglyToad.PdfPig;

namespace FileAssistant.Infrastructure.Tests;

[Trait("Category", "Office")]
[Collection("Office")]
public sealed class OfficeNextStageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-office-next-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileProcessingService service;
    public OfficeNextStageTests()
    {
        Directory.CreateDirectory(root);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "FileAssistant.slnx"))) repository = repository.Parent;
        var office = Path.Combine(repository!.FullName, "artifacts", "dependencies", "libreoffice", EnginePackageCatalog.LibreOffice.Version, "extracted", "program", "soffice.com");
        Assert.True(File.Exists(office), "Run scripts/test.ps1 -Office to prepare the optional engine.");
        service = new(new("unused", Path.Combine(root, "cache"), OfficeExecutableOverride: office));
    }
    private async Task<ConversionReport> Convert(string source, ConversionTarget target, ConversionSettings? settings = null)
    {
        var request = new ConversionRequest([source], Path.Combine(root, Guid.NewGuid().ToString("N")), target, Settings: settings);
        var plan = await service.CreatePlanAsync(request, default);
        Assert.True(plan.ReadyCount > 0, string.Join(";", plan.Items.Select(item => item.Description)));
        var result = await service.ConvertAsync(plan, null, default);
        Assert.True(result.Failed == 0, string.Join(";", result.Items.Select(item => item.Message)));
        return result;
    }
    private static string Single(ConversionReport report) => Assert.Single(report.Outputs).FullPath;
    private static void Entry(ZipArchive zip, string name, string text)
    { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }

    private string Workbook()
    {
        var path = Path.Combine(root, "多表公式.ods");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Entry(zip, "mimetype", "application/vnd.oasis.opendocument.spreadsheet");
        Entry(zip, "META-INF/manifest.xml", """<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/></manifest:manifest>""");
        Entry(zip, "content.xml", """
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:of="urn:oasis:names:tc:opendocument:xmlns:of:1.2" office:version="1.2"><office:body><office:spreadsheet>
            <table:table table:name="数据"><table:table-row><table:table-cell office:value-type="float" office:value="2"><text:p>2</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="3"><text:p>3</text:p></table:table-cell><table:table-cell table:formula="of:=SUM([.A1:.B1])" office:value-type="float" office:value="5"><text:p>5</text:p></table:table-cell></table:table-row><table:table-row><table:table-cell office:value-type="string"><text:p>中文,引号"与换行</text:p><text:p>第二行</text:p></table:table-cell></table:table-row></table:table>
            <table:table table:name="Second"><table:table-row><table:table-cell office:value-type="string"><text:p>SecondSheet</text:p></table:table-cell></table:table-row></table:table>
            </office:spreadsheet></office:body></office:document-content>
            """);
        return path;
    }

    [Theory]
    [InlineData(ConversionTarget.Xls, ConversionTarget.Xlsx)]
    [InlineData(ConversionTarget.Xls, ConversionTarget.Ods)]
    [InlineData(ConversionTarget.Xlsx, ConversionTarget.Xls)]
    [InlineData(ConversionTarget.Xlsx, ConversionTarget.Ods)]
    [InlineData(ConversionTarget.Ods, ConversionTarget.Xls)]
    [InlineData(ConversionTarget.Ods, ConversionTarget.Xlsx)]
    public async Task Workbook_routes_reopen_with_sheets_formulas_values_and_pdf(ConversionTarget from, ConversionTarget to)
    {
        var original = Workbook(); var hash = SHA256.HashData(await File.ReadAllBytesAsync(original));
        var source = from == ConversionTarget.Ods ? original : Single(await Convert(original, from));
        var converted = Single(await Convert(source, to));
        var results = await Convert(converted, ConversionTarget.Csv);
        Assert.Equal(2, results.Outputs.Count);
        Assert.Contains("数据", results.Outputs[0].FullPath); Assert.Contains("Second", results.Outputs[1].FullPath);
        var values = await File.ReadAllTextAsync(results.Outputs[0].FullPath); Assert.Contains("2,3,5", values); Assert.Contains("中文", values);
        Assert.Contains("SecondSheet", await File.ReadAllTextAsync(results.Outputs[1].FullPath));
        var formula = Single(await Convert(converted, ConversionTarget.Tsv, new(Sheet: 1, ExportFormulas: true)));
        Assert.Contains("SUM", await File.ReadAllTextAsync(formula)); Assert.Contains('\t', await File.ReadAllTextAsync(formula));
        using var pdf = PdfDocument.Open(Single(await Convert(converted, ConversionTarget.Pdf)));
        Assert.True(pdf.NumberOfPages >= 2); Assert.Contains("SecondSheet", string.Concat(pdf.GetPages().Select(page => page.Text)));
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(original)));
    }

    [Theory]
    [InlineData(DelimitedEncoding.Utf8)]
    [InlineData(DelimitedEncoding.Gb18030)]
    public async Task Csv_tsv_encoding_separator_and_formula_import_are_explicit(DelimitedEncoding encoding)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var charset = encoding == DelimitedEncoding.Utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("GB18030");
        var source = Path.Combine(root, "import.csv"); await File.WriteAllTextAsync(source, "中文;3;=B1*2\r\n\"quoted;field\";4;8", charset);
        var options = new ConversionSettings(Encoding: encoding, Separator: CsvSeparator.Semicolon, ImportFormulas: true);
        var workbook = Single(await Convert(source, ConversionTarget.Xlsx, options));
        var tsv = Single(await Convert(workbook, ConversionTarget.Tsv, options));
        var text = await File.ReadAllTextAsync(tsv, charset);
        using (var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(text)))
        { parser.SetDelimiters("\t"); Assert.Equal(["中文", "3", "6"], parser.ReadFields()); }
        Assert.Contains("quoted;field", text);
        var fromTsv = Single(await Convert(tsv, ConversionTarget.Ods, options));
        Assert.Contains("中文", await File.ReadAllTextAsync(Single(await Convert(fromTsv, ConversionTarget.Csv)), Encoding.UTF8));
        var literal = Single(await Convert(source, ConversionTarget.Ods, options with { ImportFormulas = false }));
        var literalCsv = Single(await Convert(literal, ConversionTarget.Csv));
        Assert.Contains("=B1*2", await File.ReadAllTextAsync(literalCsv));
    }

    [Fact]
    public async Task Sheet_selection_invalid_sheet_and_multi_output_cancellation_are_reported()
    {
        var source = Workbook();
        var request = new ConversionRequest([source], Path.Combine(root, "outputs"), ConversionTarget.Csv);
        var plan = await service.CreatePlanAsync(request, default); Assert.Equal(2, plan.ReadyCount);
        var single = await Convert(source, ConversionTarget.Csv, new(Sheet: 2)); Assert.Contains("SecondSheet", await File.ReadAllTextAsync(Single(single)));
        var bad = await service.CreatePlanAsync(request with { Settings = new(Sheet: 3) }, default); Assert.Equal(0, bad.ReadyCount);
        using var cancel = new CancellationTokenSource();
        var report = await service.ConvertAsync(plan, new CancelAfterFirst(cancel), cancel.Token);
        Assert.True(report.Cancelled); Assert.Single(report.Outputs); Assert.True(File.Exists(report.Outputs[0].FullPath));
        Assert.Equal(1, report.Failed); Assert.Empty(Directory.GetFiles(request.OutputDirectory, "*.tmp"));
    }
    private sealed class CancelAfterFirst(CancellationTokenSource source) : IProgress<ProcessingProgress>
    { public void Report(ProcessingProgress progress) { if (progress.Completed == 1) source.Cancel(); } }

    [Fact]
    public async Task Formula_import_does_not_fetch_external_webservice_content()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start(); var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var cancellation = new CancellationTokenSource();
        var requests = 0;
        var server = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                Interlocked.Increment(ref requests);
                await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\nConnection: close\r\n\r\n7"));
            }
            catch (OperationCanceledException) { }
        });
        try
        {
            var source = Path.Combine(root, "external.csv");
            await File.WriteAllTextAsync(source, $"=WEBSERVICE(\"http://127.0.0.1:{port}/synthetic-test\")", Encoding.UTF8);
            await Convert(source, ConversionTarget.Xlsx, new(ImportFormulas: true));
        }
        finally { cancellation.Cancel(); await server; }
        Assert.Equal(0, requests);
    }

    private string Presentation()
    {
        var path = Path.Combine(root, "slides.odp"); using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Entry(zip, "mimetype", "application/vnd.oasis.opendocument.presentation");
        Entry(zip, "META-INF/manifest.xml", """<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.presentation"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Pictures/red.png" manifest:media-type="image/png"/><manifest:file-entry manifest:full-path="Object 1/" manifest:media-type="application/vnd.oasis.opendocument.chart"/><manifest:file-entry manifest:full-path="Object 1/content.xml" manifest:media-type="text/xml"/></manifest:manifest>""");
        Entry(zip, "styles.xml", """
            <office:document-styles xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" office:version="1.2"><office:styles/><office:automatic-styles><style:style style:name="Mdp1" style:family="drawing-page"><style:drawing-page-properties draw:background-size="border" draw:fill="none"/></style:style><style:page-layout style:name="PM"><style:page-layout-properties fo:page-width="25.4cm" fo:page-height="14.2875cm" style:print-orientation="landscape" fo:margin-top="0cm" fo:margin-bottom="0cm" fo:margin-left="0cm" fo:margin-right="0cm"/></style:page-layout></office:automatic-styles><office:master-styles><style:master-page style:name="TestMaster" style:page-layout-name="PM" draw:style-name="Mdp1"/></office:master-styles></office:document-styles>
            """);
        Entry(zip, "content.xml", """
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.2"><office:automatic-styles><style:style style:name="Title" style:family="paragraph"><style:text-properties fo:font-family="Arial" fo:font-size="24pt"/></style:style></office:automatic-styles><office:body><office:presentation>
            <draw:page draw:name="Slide1" draw:master-page-name="TestMaster"><draw:frame svg:x="1cm" svg:y="1cm" svg:width="20cm" svg:height="2cm"><draw:text-box><text:p text:style-name="Title">First Slide 中文</text:p></draw:text-box></draw:frame><draw:frame svg:x="1cm" svg:y="4cm" svg:width="3cm" svg:height="2cm"><draw:image xlink:href="Pictures/red.png" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame><draw:frame svg:x="10cm" svg:y="4cm" svg:width="10cm" svg:height="7cm"><draw:object xlink:href="./Object 1" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame></draw:page>
            <draw:page draw:name="Slide2" draw:master-page-name="TestMaster"><draw:frame svg:x="1cm" svg:y="1cm" svg:width="20cm" svg:height="2cm"><draw:text-box><text:p text:style-name="Title">Second Slide</text:p></draw:text-box></draw:frame></draw:page>
            </office:presentation></office:body></office:document-content>
            """);
        Entry(zip, "Object 1/content.xml", """
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:chart="urn:oasis:names:tc:opendocument:xmlns:chart:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" office:version="1.2"><office:body><office:chart><chart:chart chart:class="chart:bar" svg:width="10cm" svg:height="7cm"><chart:title><text:p>Chart Sample</text:p></chart:title><chart:plot-area chart:data-source-has-labels="both"><chart:axis chart:dimension="x" chart:name="primary-x"/><chart:axis chart:dimension="y" chart:name="primary-y"/><chart:series chart:values-cell-range-address="local-table.B2:B3" chart:label-cell-address="local-table.B1" chart:class="chart:bar"><chart:data-point chart:repeated="2"/></chart:series></chart:plot-area><table:table table:name="local-table"><table:table-header-rows><table:table-row><table:table-cell office:value-type="string"><text:p>Category</text:p></table:table-cell><table:table-cell office:value-type="string"><text:p>Values</text:p></table:table-cell></table:table-row></table:table-header-rows><table:table-rows><table:table-row><table:table-cell office:value-type="string"><text:p>A</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="3"><text:p>3</text:p></table:table-cell></table:table-row><table:table-row><table:table-cell office:value-type="string"><text:p>B</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="7"><text:p>7</text:p></table:table-cell></table:table-row></table:table-rows></table:table></chart:chart></office:chart></office:body></office:document-content>
            """);
        using var image = new MagickImage(MagickColors.Red, 60, 40); using var stream = zip.CreateEntry("Pictures/red.png").Open(); image.Write(stream, MagickFormat.Png);
        return path;
    }

    [Theory]
    [InlineData(ConversionTarget.Ppt, ConversionTarget.Pptx)]
    [InlineData(ConversionTarget.Ppt, ConversionTarget.Odp)]
    [InlineData(ConversionTarget.Pptx, ConversionTarget.Ppt)]
    [InlineData(ConversionTarget.Pptx, ConversionTarget.Odp)]
    [InlineData(ConversionTarget.Odp, ConversionTarget.Ppt)]
    [InlineData(ConversionTarget.Odp, ConversionTarget.Pptx)]
    public async Task Presentation_routes_reopen_with_two_pages_text_image_chart_and_page_size(ConversionTarget from, ConversionTarget to)
    {
        var original = Presentation(); var hash = SHA256.HashData(await File.ReadAllBytesAsync(original));
        var source = from == ConversionTarget.Odp ? original : Single(await Convert(original, from));
        var converted = Single(await Convert(source, to));
        var pdfPath = Single(await Convert(converted, ConversionTarget.Pdf));
        using var pdf = PdfDocument.Open(pdfPath); Assert.Equal(2, pdf.NumberOfPages);
        var page = pdf.GetPage(1); Assert.Contains("First Slide", page.Text); Assert.Contains("中文", page.Text); Assert.Contains("Chart Sample", page.Text);
        Assert.Contains("Second Slide", pdf.GetPage(2).Text); Assert.InRange(page.Width, 719, 721); Assert.InRange(page.Height, 404, 406);
        Assert.NotEmpty(page.Letters); Assert.All(page.Letters, letter => Assert.False(string.IsNullOrWhiteSpace(letter.FontName)));
        Assert.True(page.NumberOfImages > 0);
        var raster = await Convert(pdfPath, ConversionTarget.Png); Assert.Equal(2, raster.Outputs.Count);
        using var image = new MagickImage(raster.Outputs[0].FullPath); using var pixels = image.GetPixels();
        var red = pixels.GetPixel((int)(image.Width * 2 / 25.4), (int)(image.Height * 5 / 14.2875)).ToColor()!;
        Assert.True(red.R > 60000 && red.B < 1000);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(original)));
    }

    [Fact]
    public async Task Presentation_source_has_explicit_widescreen_layout()
    {
        using var pdf = PdfDocument.Open(Single(await Convert(Presentation(), ConversionTarget.Pdf)));
        Assert.Equal(2, pdf.NumberOfPages); Assert.InRange(pdf.GetPage(1).Width, 719, 721); Assert.InRange(pdf.GetPage(1).Height, 404, 406);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[CollectionDefinition("Office", DisableParallelization = true)]
public sealed class OfficeTestCollection;
