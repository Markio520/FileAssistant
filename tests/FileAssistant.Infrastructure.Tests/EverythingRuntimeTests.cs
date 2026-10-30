using FileAssistant.Core.Features.Search;
using FileAssistant.Infrastructure.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileAssistant.Infrastructure.Tests;

public sealed class EverythingRuntimeTests
{
    [Fact, Trait("Category", "Everything")]
    public async Task Managed_engine_starts_updates_persists_and_stops_without_touching_other_instances()
    {
        var executable = Environment.GetEnvironmentVariable("FILEASSISTANT_TEST_ENGINE")!;
        Assert.True(File.Exists(executable));
        var root = Path.Combine(Path.GetTempPath(), "FileAssistantRuntime-" + Guid.NewGuid().ToString("N"));
        var fixtures = Path.Combine(root, "文件样本");
        var data = Path.Combine(root, "索引数据");
        Directory.CreateDirectory(fixtures);
        var first = Path.Combine(fixtures, "开始.txt");
        await File.WriteAllTextAsync(first, "index fixture");
        var instance = "FileAssistantTest-" + Guid.NewGuid().ToString("N");
        var client = new EverythingSearchClient(instance);
        var other = new EverythingSearchClient("FileAssistantReview");
        var before = await other.SearchAsync(new() { PageSize = 1 }, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await using (var runtime = new EverythingRuntime(executable, data, instance, false, [fixtures]))
            {
                await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => runtime.EnsureReadyAsync(null, timeout.Token)));
                Assert.Contains((await client.SearchAsync(new(), timeout.Token)).Items, item => item.FullPath == first);
                var renamed = Path.Combine(fixtures, "改名后的文件.txt");
                File.Move(first, renamed);
                await WaitForAsync(async () => (await client.SearchAsync(new() { Text = "改名后的文件" }, timeout.Token)).Total == 1, timeout.Token);
                Assert.Equal(0, (await client.SearchAsync(new() { Text = "开始.txt" }, timeout.Token)).Total);
                await File.WriteAllTextAsync(Path.Combine(fixtures, "新建.txt"), "new file");
                await WaitForAsync(async () => (await client.SearchAsync(new() { Text = "新建.txt" }, timeout.Token)).Total == 1, timeout.Token);
            }
            await Assert.ThrowsAsync<SearchException>(() => client.SearchAsync(new(), timeout.Token));
            Assert.True(new FileInfo(Path.Combine(data, "Everything.db")).Length > 0);
            Assert.Equal(before.Total, (await other.SearchAsync(new() { PageSize = 1 }, timeout.Token)).Total);
            await using (var restarted = new EverythingRuntime(executable, data, instance, false, [fixtures]))
            {
                await restarted.EnsureReadyAsync(null, timeout.Token);
                Assert.Equal(2, (await client.SearchAsync(new() { Text = "file:" }, timeout.Token)).Total);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Invalid_binary_is_rejected_before_process_start_or_elevation()
    {
        var root = Path.Combine(Path.GetTempPath(), "FileAssistantInvalidEngine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "Everything.exe");
        await File.WriteAllTextAsync(file, "not the official engine");
        try
        {
            await using var runtime = new EverythingRuntime(file, Path.Combine(root, "data"), "FileAssistant-test");
            var error = await Assert.ThrowsAsync<SearchException>(() => runtime.EnsureReadyAsync(null, default));
            Assert.Contains("校验失败", error.Message);
            Assert.False(Directory.Exists(Path.Combine(root, "data")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact, Trait("Category", "Everything")]
    public async Task Cancelled_query_does_not_stop_indexing_and_a_crashed_process_recovers()
    {
        var executable = Environment.GetEnvironmentVariable("FILEASSISTANT_TEST_ENGINE")!;
        var root = Path.Combine(Path.GetTempPath(), "FileAssistantRecovery-" + Guid.NewGuid().ToString("N"));
        var fixtures = Path.Combine(root, "files");
        Directory.CreateDirectory(fixtures);
        await File.WriteAllTextAsync(Path.Combine(fixtures, "恢复.txt"), "fixture");
        var instance = "FileAssistantRecovery-" + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await using var runtime = new EverythingRuntime(executable, Path.Combine(root, "data"), instance, false, [fixtures]);
            var initial = runtime.EnsureReadyAsync(null, timeout.Token);
            using var cancelled = new CancellationTokenSource();
            var query = runtime.EnsureReadyAsync(null, cancelled.Token);
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
            await initial;
            var client = new EverythingSearchClient(instance);
            Assert.Equal(1, (await client.SearchAsync(new() { Text = "恢复.txt" }, timeout.Token)).Total);
            GetWindowThreadProcessId(FindWindowW($"EVERYTHING_TASKBAR_NOTIFICATION_({instance})", null), out var id);
            Assert.NotEqual(0u, id);
            using (var engine = Process.GetProcessById((int)id)) { engine.Kill(); await engine.WaitForExitAsync(timeout.Token); }
            await runtime.EnsureReadyAsync(null, timeout.Token);
            Assert.Equal(1, (await client.SearchAsync(new() { Text = "恢复.txt" }, timeout.Token)).Total);
        }
        finally { Directory.Delete(root, true); }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowW(string name, string? title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint id);

    private static async Task WaitForAsync(Func<Task<bool>> predicate, CancellationToken token)
    {
        while (!await predicate()) await Task.Delay(100, token);
    }
}
