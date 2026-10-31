using System.Diagnostics;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines;

internal static class LocalEngineProcess
{
    internal static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout,
        CancellationToken token, string failure, IReadOnlyDictionary<string, string>? environment = null)
    {
        if (!File.Exists(executable)) throw new FileProcessingException("所需本地引擎尚未安装，请前往设置 → 引擎管理。");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        if (environment is not null)
            foreach (var (key, value) in environment) start.Environment[key] = value;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        using var process = Process.Start(start) ?? throw new FileProcessingException(failure);
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(errors, output);
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new FileProcessingException(failure);
            return await output;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(errors, output);
            if (token.IsCancellationRequested) throw;
            throw new FileProcessingException(failure + "（处理超时）");
        }
    }
}
