using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Tests;

public sealed class SearchRangeTests
{
    [Fact]
    public void Inclusive_size_range_keeps_both_edges_and_excludes_unknown_and_folders()
    {
        SearchFile[] files = [new(@"D:\below", false, 9, null), new(@"D:\min", false, 10, null),
            new(@"D:\max", false, 20, null), new(@"D:\above", false, 21, null),
            new(@"D:\unknown", false, null, null), new(@"D:\folder", true, null, null)];
        var request = new SearchRequest { MinimumSize = 10, MaximumSize = 20 };
        Assert.Equal(2, SearchRules.Filter(files, request, "batch").Total);
        var expression = SearchRules.Compile(request).EverythingExpression;
        Assert.Contains("size:>=10", expression);
        Assert.Contains("size:<=20", expression);
    }

    [Fact]
    public void Date_range_includes_the_full_last_day_and_has_identical_index_boundaries()
    {
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 1)));
        var before = start.AddDays(2);
        SearchFile[] files = [new(@"D:\before", false, 1, start.AddTicks(-1)), new(@"D:\first", false, 1, start),
            new(@"D:\last", false, 1, before.AddTicks(-1)), new(@"D:\next", false, 1, before), new(@"D:\unknown", false, 1, null)];
        var request = new SearchRequest { ModifiedFrom = start, ModifiedBefore = before };
        Assert.Equal(["first", "last"], SearchRules.Filter(files, request, "batch").Items.Select(f => f.Name));
        var expression = SearchRules.Compile(request).EverythingExpression;
        Assert.Contains("dm:>=2026-10-01", expression);
        Assert.Contains("dm:<2026-10-03", expression);
        Assert.Equal(3, SearchRules.Filter(files, request with { ModifiedFrom = null }, "batch").Total);
        Assert.Equal(3, SearchRules.Filter(files, request with { ModifiedBefore = null }, "batch").Total);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, -1)]
    [InlineData(20, 10)]
    public void Invalid_size_ranges_fail_before_search(long minimum, long maximum)
        => Assert.Throws<SearchException>(() => SearchRules.Compile(new() { MinimumSize = minimum, MaximumSize = maximum }));

    [Fact]
    public void Empty_or_reversed_date_range_is_rejected()
    {
        var today = DateTimeOffset.Now;
        Assert.Throws<SearchException>(() => SearchRules.Compile(new() { ModifiedFrom = today, ModifiedBefore = today }));
        Assert.Throws<SearchException>(() => SearchRules.Compile(new() { ModifiedFrom = today, ModifiedBefore = today.AddDays(-1) }));
    }
}
