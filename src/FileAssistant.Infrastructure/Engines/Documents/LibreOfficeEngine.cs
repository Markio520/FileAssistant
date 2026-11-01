using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines.Documents;

internal sealed class LibreOfficeEngine(string executable)
{
    internal async Task<IReadOnlyList<string>> GetSheetsAsync(string path, ConversionRequest request, CancellationToken token)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".csv" or ".tsv") return ["Sheet1"];
        if (extension is ".xlsx" or ".ods") return ReadSheets(path, extension);
        using var temporary = new TemporaryDirectory();
        var converted = Path.Combine(temporary.Path, "sheets.ods");
        await ConvertAsync(path, converted, request with { Target = ConversionTarget.Ods }, null, token);
        return ReadSheets(converted, ".ods");
    }

    private static IReadOnlyList<string> ReadSheets(string path, string extension)
    {
        using var zip = ZipFile.OpenRead(path);
        using var stream = (zip.GetEntry(extension == ".xlsx" ? "xl/workbook.xml" : "content.xml")
            ?? throw new FileProcessingException("无法读取工作表目录。文件可能损坏。")).Open();
        var document = XDocument.Load(stream);
        XNamespace ns = extension == ".xlsx" ? "http://schemas.openxmlformats.org/spreadsheetml/2006/main" : "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        return document.Descendants(ns + (extension == ".xlsx" ? "sheet" : "table"))
            .Select(sheet => (string?)sheet.Attribute(extension == ".xlsx" ? "name" : ns + "name") ?? "未命名").ToArray();
    }

    internal async Task ConvertAsync(string path, string output, ConversionRequest request, int? sheet, CancellationToken token)
    {
        using var temporary = new TemporaryDirectory();
        var root = temporary.Path;
        var target = request.Target;
        {
            var inputDirectory = Path.Combine(root, "input");
            var outputDirectory = Path.Combine(root, "output");
            var profile = Path.Combine(root, "profile");
            Directory.CreateDirectory(inputDirectory); Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(Path.Combine(profile, "user"));
            // A private copy also keeps LibreOffice's source lock files out of the user's directory.
            var input = Path.Combine(inputDirectory, "document" + Path.GetExtension(path).ToLowerInvariant());
            await using (var source = File.OpenRead(path))
            await using (var copy = File.Create(input)) await source.CopyToAsync(copy, token);
            await File.WriteAllTextAsync(Path.Combine(profile, "user", "registrymodifications.xcu"), """
                <?xml version="1.0" encoding="UTF-8"?>
                <oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema">
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Writer/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Calc/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>1</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Impress/Misc"><prop oor:name="UpdateFromTemplate" oor:op="fuse"><value>false</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Jobs/Jobs/org.openoffice.Office.Jobs:Job['UpdateCheck']/Arguments"><prop oor:name="AutoCheckEnabled" oor:op="fuse" oor:type="xs:boolean"><value>false</value></prop><prop oor:name="AutoDownloadEnabled" oor:op="fuse" oor:type="xs:boolean"><value>false</value></prop></item>
                </oor:items>
                """, Encoding.UTF8, token);
            var filter = target switch
            {
                ConversionTarget.Doc => "doc:MS Word 97", ConversionTarget.Docx => "docx:Office Open XML Text",
                ConversionTarget.Odt => "odt:writer8", ConversionTarget.Rtf => "rtf:Rich Text Format",
                ConversionTarget.Txt => "txt:Text (encoded):UTF8",
                ConversionTarget.Pdf => ConversionCapabilities.IsSpreadsheet(path) ? "pdf:calc_pdf_Export" : ConversionCapabilities.IsPresentation(path) ? "pdf:impress_pdf_Export" : "pdf:writer_pdf_Export",
                ConversionTarget.Xls => "xls:MS Excel 97", ConversionTarget.Xlsx => "xlsx:Calc MS Excel 2007 XML", ConversionTarget.Ods => "ods:calc8",
                ConversionTarget.Csv or ConversionTarget.Tsv => "csv:Text - txt - csv (StarCalc):" + CsvOptions(request.Options, target == ConversionTarget.Tsv, sheet ?? 1),
                ConversionTarget.Ppt => "ppt:MS PowerPoint 97", ConversionTarget.Pptx => "pptx:Impress MS PowerPoint 2007 XML", ConversionTarget.Odp => "odp:impress8",
                _ => throw new FileProcessingException("不支持此文档输出格式。")
            };
            var arguments = new List<string> { "-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri,
                "--headless", "--nologo", "--nodefault", "--nofirststartwizard", "--norestore" };
            if (Path.GetExtension(input) == ".txt") arguments.Add("--infilter=Text (encoded):UTF8");
            if (Path.GetExtension(input) is ".csv" or ".tsv") arguments.Add("--infilter=Text - txt - csv (StarCalc):" + CsvOptions(request.Options, Path.GetExtension(input) == ".tsv", 1, importing: true));
            arguments.AddRange(["--convert-to", filter, "--outdir", outputDirectory, input]);
            await LocalEngineProcess.RunAsync(executable, arguments, TimeSpan.FromMinutes(5), token, "文档转换失败，文件可能损坏、受密码保护或格式不支持。",
                new Dictionary<string, string>
                {
                    ["PATH"] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "System64")) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                    ["SAL_DISABLE_OPENCL"] = "1"
                });
            var generated = target is ConversionTarget.Csv or ConversionTarget.Tsv
                ? Directory.GetFiles(outputDirectory, "*.csv").SingleOrDefault() ?? ""
                : Path.Combine(outputDirectory, "document" + ConversionCapabilities.Extension(target));
            if (!File.Exists(generated) || new FileInfo(generated).Length == 0 && target is not (ConversionTarget.Csv or ConversionTarget.Tsv))
                throw new FileProcessingException("文档引擎未生成输出，请检查文件是否损坏或受密码保护。");
            token.ThrowIfCancellationRequested();
            File.Move(generated, output, overwrite: false);
        }
    }

    // Explicit import locale and all 15 options avoid dependence on machine defaults.
    private static string CsvOptions(ConversionSettings settings, bool tsv, int sheet, bool importing = false)
    {
        var separator = tsv ? 9 : settings.Separator switch { CsvSeparator.Semicolon => 59, CsvSeparator.Tab => 9, _ => 44 };
        var encoding = settings.Encoding == DelimitedEncoding.Gb18030 ? 85 : 76;
        return $"{separator},34,{encoding},1,,1033,true,{(!importing).ToString().ToLowerInvariant()},false,{settings.ExportFormulas.ToString().ToLowerInvariant()},false,{sheet},{settings.ImportFormulas.ToString().ToLowerInvariant()},false,false";
    }
}
