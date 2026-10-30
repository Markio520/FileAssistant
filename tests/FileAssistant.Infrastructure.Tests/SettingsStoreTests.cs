using FileAssistant.Infrastructure.Persistence;

namespace FileAssistant.Infrastructure.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FileAssistant-settings-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Settings_survive_new_store_instances_and_replace_the_previous_document()
    {
        var path = Path.Combine(directory, "search.json");
        var first = new JsonSettingsStore(path);
        Assert.Null(await first.ReadAsync());
        await first.WriteAsync("{\"name\":\"常用条件\"}");
        var reopened = new JsonSettingsStore(path);
        Assert.Equal("{\"name\":\"常用条件\"}", await reopened.ReadAsync());
        await reopened.WriteAsync("{\"sort\":7}");
        Assert.Equal("{\"sort\":7}", await first.ReadAsync());
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task Failed_commit_does_not_leave_a_temporary_settings_file()
    {
        var path = Path.Combine(directory, "search.json");
        Directory.CreateDirectory(path); // Simulate an unusable destination without touching user settings.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new JsonSettingsStore(path).WriteAsync("{}"));
        Assert.Empty(Directory.GetFiles(directory));
        Assert.True(Directory.Exists(path));
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(directory);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(full).StartsWith("FileAssistant-settings-", StringComparison.Ordinal) && Directory.Exists(full))
            Directory.Delete(full, recursive: true);
    }
}
