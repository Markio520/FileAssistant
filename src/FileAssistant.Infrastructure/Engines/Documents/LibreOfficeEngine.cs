using System.Text;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines.Documents;

internal sealed class LibreOfficeEngine(string executable)
{
    internal async Task ConvertAsync(string path, string output, ConversionTarget target, CancellationToken token)
    {
        var root = Path.Combine(Path.GetTempPath(), "FileAssistant-office-" + Guid.NewGuid().ToString("N"));
        try
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
                  <item oor:path="/org.openoffice.Office.Writer/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Jobs/Jobs/org.openoffice.Office.Jobs:Job['UpdateCheck']/Arguments"><prop oor:name="AutoCheckEnabled" oor:op="fuse" oor:type="xs:boolean"><value>false</value></prop><prop oor:name="AutoDownloadEnabled" oor:op="fuse" oor:type="xs:boolean"><value>false</value></prop></item>
                </oor:items>
                """, Encoding.UTF8, token);
            var filter = target switch
            {
                ConversionTarget.Doc => "doc:MS Word 97", ConversionTarget.Docx => "docx:Office Open XML Text",
                ConversionTarget.Odt => "odt:writer8", ConversionTarget.Rtf => "rtf:Rich Text Format",
                ConversionTarget.Txt => "txt:Text (encoded):UTF8", ConversionTarget.Pdf => "pdf:writer_pdf_Export",
                _ => throw new FileProcessingException("不支持此文档输出格式。")
            };
            var arguments = new List<string> { "-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri,
                "--headless", "--nologo", "--nodefault", "--nofirststartwizard", "--norestore" };
            if (Path.GetExtension(input) == ".txt") arguments.Add("--infilter=Text (encoded):UTF8");
            arguments.AddRange(["--convert-to", filter, "--outdir", outputDirectory, input]);
            await LocalEngineProcess.RunAsync(executable, arguments, TimeSpan.FromMinutes(5), token, "文档转换失败，文件可能损坏、受密码保护或格式不支持。",
                new Dictionary<string, string>
                {
                    ["PATH"] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "System64")) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                    ["SAL_DISABLE_OPENCL"] = "1"
                });
            var generated = Path.Combine(outputDirectory, "document" + ConversionCapabilities.Extension(target));
            if (!File.Exists(generated) || new FileInfo(generated).Length == 0)
                throw new FileProcessingException("文档引擎未生成输出，请检查文件是否损坏或受密码保护。");
            token.ThrowIfCancellationRequested();
            File.Move(generated, output, overwrite: false);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
