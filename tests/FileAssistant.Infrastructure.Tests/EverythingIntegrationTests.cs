using FileAssistant.Core.Features.Search;
using FileAssistant.Infrastructure.Windows;

namespace FileAssistant.Infrastructure.Tests;

/// <summary>Run explicitly with scripts/test.ps1 -Everything after preparing the isolated review instance.</summary>
public sealed class EverythingIntegrationTests
{
    private readonly EverythingSearchClient client = new("FileAssistantReview");
    [Fact, Trait("Category", "Everything")]
    public async Task Real_ipc_returns_chinese_names_metadata_and_paged_results()
    {
        var page = await client.SearchAsync(new() { Text = "file:", PageSize = 2 }, default);
        Assert.True(page.Total >= 300);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, f => { Assert.NotNull(f.Size); Assert.NotNull(f.Modified); Assert.True(File.Exists(f.FullPath)); });
        var next = await client.SearchAsync(new() { Text = "file:", Offset = 2, PageSize = 2 }, default);
        Assert.DoesNotContain(next.Items[0].FullPath, page.Items.Select(f => f.FullPath));
        var chinese = await client.SearchAsync(new() { Text = "产品 主图 ext:png" }, default);
        Assert.Contains(chinese.Items, f => f.Name.Contains("产品 主图"));
    }
    [Theory, Trait("Category", "Everything")]
    [InlineData("ext:txt", SearchKind.All)]
    [InlineData("产品 | 报价", SearchKind.Images)]
    [InlineData("!备份", SearchKind.All)]
    [InlineData("size:>1kb", SearchKind.Files)]
    [InlineData("dm:today", SearchKind.Files)]
    [InlineData("*.png", SearchKind.Images)]
    [InlineData("", SearchKind.Audio)]
    [InlineData("", SearchKind.Archives)]
    [InlineData("", SearchKind.Documents)]
    [InlineData("", SearchKind.Executables)]
    [InlineData("", SearchKind.Folders)]
    [InlineData("", SearchKind.Video)]
    public async Task Engine_and_local_rules_agree(string text, SearchKind kind)
    {
        var all = await client.SearchAsync(new() { PageSize = 1000 }, default);
        Assert.True(all.Total >= 300, "The isolated Everything fixture index must be loaded.");
        var request = new SearchRequest { Text = text, Kind = kind, PageSize = 1000 };
        var indexed = await client.SearchAsync(request, default);
        var local = SearchRules.Filter(all.Items, request, "test");
        Assert.Equal(local.Items.Select(f => f.FullPath).Order(), indexed.Items.Select(f => f.FullPath).Order());
    }
    [Fact, Trait("Category", "Everything")]
    public async Task Engine_sort_is_global_before_page_limit()
    {
        var all = await client.SearchAsync(new() { Kind = SearchKind.Files, PageSize = 1000 }, default);
        var largest = await client.SearchAsync(new() { Kind = SearchKind.Files, Sort = SearchSort.Size, Descending = true, PageSize = 1 }, default);
        Assert.Equal(all.Items.Max(f => f.Size), Assert.Single(largest.Items).Size);
    }
}
