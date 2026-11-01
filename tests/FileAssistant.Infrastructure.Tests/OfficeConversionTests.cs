using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;

namespace FileAssistant.Infrastructure.Tests;

[Trait("Category", "Office")]
[Collection("Office")]
public sealed class OfficeConversionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-office-test-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileProcessingService service;
    private readonly string repository;
    public OfficeConversionTests()
    {
        Directory.CreateDirectory(root);
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FileAssistant.slnx"))) current = current.Parent;
        repository = current!.FullName;
        var office = Path.Combine(repository, "artifacts", "dependencies", "libreoffice", EnginePackageCatalog.LibreOffice.Version, "extracted", "program", "soffice.com");
        Assert.True(File.Exists(office), "Run scripts/test.ps1 -Office to prepare the optional engine.");
        service = new(new("unused", Path.Combine(root, "cache"), OfficeExecutableOverride: office));
    }
    private string Document()
    {
        var path = Path.Combine(root, "原件 中文 & 空格.docx");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); writer.Write(content); }
        Entry("[Content_Types].xml", """<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
        Entry("_rels/.rels", """<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
        Entry("word/document.xml", """<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>FileAssistantSample 中文内容</w:t></w:r></w:p><w:p><w:r><w:t>Second paragraph.</w:t></w:r></w:p><w:sectPr><w:pgSz w:w="11906" w:h="16838"/></w:sectPr></w:body></w:document>""");
        return path;
    }

    [Theory]
    [InlineData(ConversionTarget.Doc)]
    [InlineData(ConversionTarget.Docx)]
    [InlineData(ConversionTarget.Odt)]
    [InlineData(ConversionTarget.Rtf)]
    [InlineData(ConversionTarget.Txt)]
    [InlineData(ConversionTarget.Pdf)]
    public async Task Document_targets_are_real_files_with_text_and_preserve_the_original(ConversionTarget target)
    {
        var source = Document(); var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "out"), target), default);
        Assert.Equal(1, plan.ReadyCount);
        var report = await service.ConvertAsync(plan, null, default);
        Assert.True(report.Outputs.Count == 1, string.Join(";", report.Items.Select(item => item.Message)));
        var output = Assert.Single(report.Outputs);
        var bytes = await File.ReadAllBytesAsync(output.FullPath);
        if (target == ConversionTarget.Pdf)
        {
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes[..4]));
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(output.FullPath);
            Assert.Contains("FileAssistantSample", pdf.GetPage(1).Text);
        }
        else
        {
            if (target == ConversionTarget.Doc) Assert.Equal(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }, bytes[..4]);
            if (target is ConversionTarget.Docx or ConversionTarget.Odt)
            {
                using var zip = ZipFile.OpenRead(output.FullPath);
                Assert.NotNull(zip.GetEntry(target == ConversionTarget.Docx ? "word/document.xml" : "content.xml"));
            }
            var roundtrip = await service.CreatePlanAsync(new([output.FullPath], Path.Combine(root, "txt"), ConversionTarget.Txt), default);
            var text = Assert.Single((await service.ConvertAsync(roundtrip, null, default)).Outputs);
            var content = await File.ReadAllTextAsync(text.FullPath, Encoding.UTF8);
            Assert.Contains("FileAssistantSample", content); Assert.Contains("中文内容", content);
        }
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        Assert.Single(Directory.GetFiles(root, "*.docx"));
        Assert.Empty(Directory.GetFiles(root, ".~lock*"));
    }

    [Fact]
    public async Task Cancellation_stops_office_and_a_later_conversion_can_succeed()
    {
        var source = Document(); var hash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var plan = await service.CreatePlanAsync(new([source], Path.Combine(root, "cancelled"), ConversionTarget.Pdf), default);
        using var cancellation = new CancellationTokenSource(); cancellation.CancelAfter(500);
        var stopped = await service.ConvertAsync(plan, null, cancellation.Token);
        Assert.True(stopped.Cancelled); Assert.Empty(stopped.Outputs);
        Assert.False(Directory.Exists(plan.Request.OutputDirectory) && Directory.EnumerateFiles(plan.Request.OutputDirectory).Any());
        var retry = await service.CreatePlanAsync(new([source], Path.Combine(root, "retry"), ConversionTarget.Pdf), default);
        var report = await service.ConvertAsync(retry, null, default);
        Assert.True(report.Outputs.Count == 1, string.Join(";", report.Items.Select(item => item.Message)));
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    [Fact]
    public async Task Verified_office_package_installs_privately_and_preserves_licenses()
    {
        var package = EnginePackageCatalog.LibreOffice;
        var archive = Path.Combine(repository, "artifacts", "dependencies", "libreoffice", package.Version, Path.GetFileName(package.DownloadUri.LocalPath));
        using var client = new HttpClient(new ArchiveHandler(archive));
        var directory = Path.Combine(root, "engines");
        var installed = await new EnginePackageInstaller(client, directory).InstallAsync(package, null, default);
        Assert.Equal(installed, EnginePackageInstaller.FindInstalled(directory, package));
        var installation = Path.GetFullPath(Path.Combine(installed, "..", ".."));
        Assert.True(File.Exists(Path.Combine(installation, "license.txt")));
        Assert.True(File.Exists(Path.Combine(installation, "NOTICE")));
        Assert.Empty(Directory.GetFiles(installation, "*.msi"));
        var processing = new LocalFileProcessingService(new("unused", Path.Combine(root, "preview"), directory));
        var text = Path.Combine(root, "text.txt"); await File.WriteAllTextAsync(text, "A private installed engine 中文", Encoding.UTF8);
        var plan = await processing.CreatePlanAsync(new([text], Path.Combine(root, "out"), ConversionTarget.Docx), default);
        Assert.Single((await processing.ConvertAsync(plan, null, default)).Outputs);
    }
    private sealed class ArchiveHandler(string path) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(path)) });
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
