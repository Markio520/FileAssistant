using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines;

internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FileAssistant-convert-" + Guid.NewGuid().ToString("N"));
    internal TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new FileProcessingException($"临时目录清理失败，请关闭占用后清理：{Path}", error); }
    }
}
