using System.Diagnostics;
using System.Text;
using FileAssistant.Core.Features.Search;
using static FileAssistant.Infrastructure.Windows.EverythingNative;

namespace FileAssistant.Infrastructure.Windows;

/// <summary>Owns one isolated index and process; cancellation of a query never cancels indexing.</summary>
public sealed class EverythingRuntime(string executable, string dataDirectory, string instance,
    bool indexVolumes = true, IReadOnlyList<string>? folders = null) : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task? startup;
    private Process? process;
    private FileStream? ownership;
    private bool disposed;
    private volatile string status = "正在启动文件索引…";

    public string Instance => instance;

    public async Task EnsureReadyAsync(IProgress<SearchProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task ready;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (startup?.IsCompletedSuccessfully == true && process?.HasExited != false) startup = null;
            ready = startup ??= Task.Run(StartCoreAsync);
        }
        while (!ready.IsCompleted)
        {
            progress?.Report(new(0, string.Empty, status));
            await Task.WhenAny(ready, Task.Delay(400, token)).WaitAsync(token);
        }
        await ready.WaitAsync(token);
    }

    public void RetryInitialization()
    {
        lock (gate)
            if (startup?.IsFaulted == true || startup?.IsCanceled == true) startup = null;
    }

    private async Task StartCoreAsync()
    {
        var token = lifetime.Token;
        try
        {
            EverythingDistribution.VerifyExecutable(executable);
            Directory.CreateDirectory(dataDirectory);
            lock (gate)
            {
                token.ThrowIfCancellationRequested();
                if (ownership is null)
                {
                    try { ownership = new(Path.Combine(dataDirectory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                    catch (IOException) { throw new SearchException("另一个文件助手窗口正在使用索引，请在已有窗口搜索，或关闭它后重试。"); }
                }
                if (process?.HasExited != false)
                {
                    process?.Dispose();
                    process = FindOwnedProcess();
                }
            }
            if (indexVolumes)
            {
                status = "正在准备全盘索引权限 · 首次启用请允许 Windows 授权";
                await EverythingServiceSetup.EnsureRunningAsync(instance, token);
            }
            token.ThrowIfCancellationRequested();
            if (process?.HasExited != false)
            {
                var config = Path.Combine(dataDirectory, "Everything.ini");
                WriteConfiguration(config);
                var start = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = dataDirectory
                };
                foreach (var value in new[] { "-instance", instance, "-config", config, "-db", Path.Combine(dataDirectory, "Everything.db"), "-startup" }) start.ArgumentList.Add(value);
                lock (gate)
                {
                    token.ThrowIfCancellationRequested();
                    process?.Dispose();
                    process = Process.Start(start) ?? throw new SearchException("无法启动内置索引，请点击“重试搜索”。");
                }
            }
            status = "正在准备文件索引 · 首次建立索引需要一些时间";
            var watch = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new SearchException("内置索引意外退出。请点击“重试搜索”重新启动。");
                var window = FindWindowW($"EVERYTHING_TASKBAR_NOTIFICATION_({instance})", null);
                if (window != 0 && SendMessageTimeoutW(window, 0x400, 401, 0, 2, 1000, out var loaded) != 0 && loaded != 0) break;
                if (watch.Elapsed > TimeSpan.FromMinutes(10)) throw new SearchException("索引仍未准备完成。可稍后点击“重试搜索”，或先用“指定目录”。");
                await Task.Delay(250, token);
            }
            status = "文件索引已就绪";
        }
        catch (Exception e) when (e is not SearchException and not OperationCanceledException)
        {
            throw new SearchException("无法准备内置索引。请检查磁盘可用空间与应用数据目录权限后重试。");
        }
    }

    private void WriteConfiguration(string filename)
    {
        // Retain the engine's saved volume IDs and journal checkpoints between launches.
        var lines = File.Exists(filename) ? File.ReadAllLines(filename).ToList() : new List<string> { "[Everything]" };
        void Set(string key, string value)
        {
            lines.RemoveAll(line => line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
            lines.Add(key + "=" + value);
        }
        foreach (var key in new[] { "app_data", "run_as_admin", "show_tray_icon", "check_for_updates_on_startup", "http_server_enabled", "etp_server_enabled", "show_ntfs_system_volumes", "show_service_missing_warning" }) Set(key, "0");
        foreach (var key in new[] { "run_in_background", "index_size", "index_date_modified", "fast_size_sort", "fast_date_modified_sort" }) Set(key, "1");
        Set("auto_include_fixed_volumes", indexVolumes ? "1" : "0");
        Set("auto_include_removable_volumes", indexVolumes ? "1" : "0");
        // In 1.4, -service-pipe-name is a configuration command that saves settings and exits.
        // Supply it in the INI so the normal -startup invocation remains running.
        if (indexVolumes) Set("service_pipe_name", EverythingDistribution.ServicePipe(instance));
        if (!indexVolumes)
            foreach (var key in new[] { "ntfs_volume_guids", "ntfs_volume_paths", "ntfs_volume_includes" }) Set(key, string.Empty);
        var scannedFolders = folders ?? (indexVolumes ? GetOtherLocalVolumes() : []);
        Set("folders", string.Join(',', scannedFolders.Select(path => '"' + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + '"')));
        Set("folder_monitor_changes", string.Join(',', scannedFolders.Select(_ => "1")));
        Set("folder_update_types", string.Join(',', scannedFolders.Select(_ => "0")));
        File.WriteAllLines(filename, lines, new UTF8Encoding(false));
    }

    private static IReadOnlyList<string> GetOtherLocalVolumes()
    {
        var result = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is DriveType.Fixed or DriveType.Removable && drive.IsReady && !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)) result.Add(drive.RootDirectory.FullName);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result;
    }

    private Process? FindOwnedProcess()
    {
        var window = FindWindowW($"EVERYTHING_TASKBAR_NOTIFICATION_({instance})", null);
        if (window == 0) return null;
        GetWindowThreadProcessId(window, out var id);
        var found = Process.GetProcessById((int)id);
        if (!string.Equals(found.MainModule?.FileName, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
        {
            found.Dispose();
            throw new SearchException("内置索引实例被其他程序占用。请重启本软件后重试。");
        }
        return found;
    }

    public async ValueTask DisposeAsync()
    {
        Process? owned;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            owned = process;
        }
        try
        {
            if (owned?.HasExited == false)
            {
                var stop = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                foreach (var value in new[] { "-instance", instance, "-exit" }) stop.ArgumentList.Add(value);
                using var command = Process.Start(stop);
                try { await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
                catch (TimeoutException) { owned.Kill(); await owned.WaitForExitAsync(); }
            }
        }
        finally { owned?.Dispose(); ownership?.Dispose(); }
    }
}
