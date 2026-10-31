using System.Diagnostics;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines.Media;

internal sealed class FfmpegEngine(string executable)
{
    internal static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v" };

    internal async Task PreviewAsync(string path, string output, int maximumEdge, CancellationToken token)
    {
        if (!File.Exists(executable)) throw new FileProcessingException("视频引擎尚未安装，请在引擎管理中检查组件。");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        foreach (var argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-protocol_whitelist", "file,pipe",
            "-i", path, "-map", "0:V:0", "-frames:v", "1", "-an", "-sn", "-vf",
            $"scale={maximumEdge}:{maximumEdge}:force_original_aspect_ratio=decrease", "-f", "image2", "-update", "1", output })
            start.ArgumentList.Add(argument);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var process = Process.Start(start) ?? throw new FileProcessingException("无法启动视频引擎。");
        var errors = process.StandardError.ReadToEndAsync();
        var standard = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(errors, standard);
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(output)) throw new FileProcessingException("无法提取视频首帧，格式可能不支持或文件已损坏。");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            if (token.IsCancellationRequested) throw;
            throw new FileProcessingException("视频首帧读取超时，请尝试较小的文件。");
        }
    }
}
