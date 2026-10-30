using FileAssistant.Core.Features.Search;
using FileAssistant.Infrastructure.FileSystem;
using FileAssistant.Infrastructure.Windows;

namespace FileAssistant.Infrastructure.Tests;

public sealed class DirectorySearchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-search-" + Guid.NewGuid().ToString("N"));
    public DirectorySearchTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "子目录"));
        File.WriteAllText(Path.Combine(root, "报价单.txt"), "test");
        File.WriteAllText(Path.Combine(root, "子目录", "产品 主图.png"), "fixture");
    }
    [Fact] public async Task Recursive_and_direct_scopes_differ()
    {
        var index = new DirectorySearchIndex();
        Assert.Equal(3, (await index.SearchAsync(new() { Folder = root }, null, default)).Total);
        Assert.Equal(2, (await index.SearchAsync(new() { Folder = root, IncludeSubfolders = false }, null, default)).Total);
    }
    [Fact] public async Task Refresh_invalidates_snapshot()
    {
        var index = new DirectorySearchIndex();
        var request = new SearchRequest { Folder = root, Kind = SearchKind.Files };
        Assert.Equal(2, (await index.SearchAsync(request, null, default)).Total);
        File.WriteAllText(Path.Combine(root, "新增.txt"), "new");
        Assert.Equal(2, (await index.SearchAsync(request, null, default)).Total);
        index.Invalidate();
        Assert.Equal(3, (await index.SearchAsync(request, null, default)).Total);
    }
    [Fact] public async Task Missing_folder_is_error_not_empty_success()
        => await Assert.ThrowsAsync<SearchException>(() => new DirectorySearchIndex().SearchAsync(new() { Folder = Path.Combine(root, "missing") }, null, default));
    [Fact] public async Task Cancellation_can_be_followed_by_a_successful_search()
    {
        var index = new DirectorySearchIndex();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.SearchAsync(new() { Folder = root }, null, new(true)));
        Assert.Equal(3, (await index.SearchAsync(new() { Folder = root }, null, default)).Total);
    }
    [Fact] public async Task Missing_everything_instance_reports_actionable_error()
    {
        var error = await Assert.ThrowsAsync<SearchException>(() => new EverythingSearchClient(Guid.NewGuid().ToString()).SearchAsync(new(), default));
        Assert.Contains("指定目录", error.Message);
    }
    public void Dispose()
    {
        var path = Path.GetFullPath(root);
        if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("FileAssistant-search-", StringComparison.Ordinal))
            Directory.Delete(path, true);
    }
}
