using System.Text.Json;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines;

public static class EnginePackageCatalog
{
    public static EnginePackage Ffmpeg { get; } = ReadFfmpeg();
    private static EnginePackage ReadFfmpeg()
    {
        using var stream = typeof(EnginePackageCatalog).Assembly.GetManifestResourceStream("FileAssistant.EngineManifest.json")!;
        using var document = JsonDocument.Parse(stream);
        var entry = document.RootElement.GetProperty("ffmpeg");
        return new("ffmpeg", "FFmpeg", entry.GetProperty("version").GetString()!,
            new(entry.GetProperty("url").GetString()!), entry.GetProperty("sha256").GetString()!,
            Path.GetFileNameWithoutExtension(entry.GetProperty("archive").GetString()!), "bin/ffmpeg.exe",
            entry.GetProperty("downloadBytes").GetInt64(), entry.GetProperty("license").GetString()!);
    }
}
