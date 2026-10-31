using System.Text.Json;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines;

public static class EnginePackageCatalog
{
    public static EnginePackage Ffmpeg { get; } = Read("ffmpeg", "FFmpeg", "bin/ffmpeg.exe");
    public static EnginePackage LibreOffice { get; } = Read("libreoffice", "LibreOffice 文档引擎", "program/soffice.com", EngineArchiveKind.WindowsInstaller);
    private static EnginePackage Read(string id, string name, string executable, EngineArchiveKind kind = EngineArchiveKind.Zip)
    {
        using var stream = typeof(EnginePackageCatalog).Assembly.GetManifestResourceStream("FileAssistant.EngineManifest.json")!;
        using var document = JsonDocument.Parse(stream);
        var entry = document.RootElement.GetProperty(id);
        return new(id, name, entry.GetProperty("version").GetString()!,
            new(entry.GetProperty("url").GetString()!), entry.GetProperty("sha256").GetString()!,
            Path.GetFileNameWithoutExtension(entry.GetProperty("archive").GetString()!), executable,
            entry.GetProperty("downloadBytes").GetInt64(), entry.GetProperty("license").GetString()!, kind);
    }
}
