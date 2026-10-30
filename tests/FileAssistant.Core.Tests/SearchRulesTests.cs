using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Tests;

public sealed class SearchRulesTests
{
    [Theory]
    [InlineData(SearchKind.Audio, "歌曲.MP3")]
    [InlineData(SearchKind.Archives, "打包.zip")]
    [InlineData(SearchKind.Documents, "文档.pdf")]
    [InlineData(SearchKind.Executables, "安装.exe")]
    [InlineData(SearchKind.Folders, "文件夹.exe")]
    [InlineData(SearchKind.Images, "图片.png")]
    [InlineData(SearchKind.Video, "视频.mp4")]
    public void Type_filters_select_their_files_and_keep_extension_named_folders_separate(SearchKind kind, string expected)
    {
        SearchFile[] examples =
        [
            new(@"D:\样本\歌曲.MP3", false, 1, null), new(@"D:\样本\打包.zip", false, 1, null),
            new(@"D:\样本\文档.pdf", false, 1, null), new(@"D:\样本\安装.exe", false, 1, null),
            new(@"D:\样本\文件夹.exe", true, null, null), new(@"D:\样本\图片.png", false, 1, null),
            new(@"D:\样本\视频.mp4", false, 1, null), new(@"D:\样本\其他.bin", false, 1, null)
        ];
        var page = SearchRules.Filter(examples, new() { Kind = kind }, "test");
        Assert.Equal(expected, Assert.Single(page.Items).Name);
        Assert.Equal(8, SearchRules.Filter(examples, new() { Kind = SearchKind.All }, "test").Total);
        Assert.Equal(7, SearchRules.Filter(examples, new() { Kind = SearchKind.Files }, "test").Total);
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("MSI")]
    [InlineData("com")]
    [InlineData("bat")]
    [InlineData("cmd")]
    public void Executable_filter_includes_installers_and_windows_command_files(string extension)
    {
        var search = SearchRules.Compile(new() { Kind = SearchKind.Executables });
        Assert.True(search.Matches(new(@"D:\示例." + extension, false, 1, null)));
        Assert.False(search.Matches(new(@"D:\示例." + extension, true, null, null)));
        Assert.False(search.Matches(new(@"D:\说明.txt", false, 1, null)));
    }

    private static readonly SearchFile[] Files =
    [
        new(@"D:\素材\产品 主图.png", false, 3_000_000, DateTimeOffset.Now),
        new(@"D:\备份\产品.jpg", false, 100, DateTimeOffset.Now.AddDays(-40)),
        new(@"D:\文档\报价.PDF", false, 1000, DateTimeOffset.Now),
        new(@"D:\素材\无大小.bin", false, null, null),
        new(@"D:\素材\文件夹", true, null, DateTimeOffset.Now)
    ];

    [Theory]
    [InlineData("产品 主图", 1)]
    [InlineData("\"产品 主图\"", 1)]
    [InlineData("ext:png;jpg", 2)]
    [InlineData("*.PDF", 1)]
    [InlineData("*.pdf | *.png", 2)]
    [InlineData("!产品", 3)]
    [InlineData("size:>1mb", 1)]
    [InlineData("size:<=100", 1)]
    [InlineData("dm:today", 3)]
    [InlineData("dm:7days", 3)]
    [InlineData("folder:", 1)]
    [InlineData("file:", 4)]
    [InlineData("\\文档", 1)]
    public void Common_queries_match_expected_files(string text, int count)
        => Assert.Equal(count, SearchRules.Filter(Files, new() { Text = text }, "test").Total);

    [Theory]
    [InlineData("content:abc")]
    [InlineData("regex:.*")]
    [InlineData("\"未闭合")]
    [InlineData("产品 |")]
    [InlineData("| 产品")]
    [InlineData("!")]
    [InlineData("size:large")]
    [InlineData("size:999999999999999999999999999999gb")]
    [InlineData("ext:")]
    [InlineData("dm:yesterday")]
    public void Unsupported_or_invalid_syntax_is_not_silently_ignored(string text)
        => Assert.Throws<SearchException>(() => SearchRules.Compile(new() { Text = text }));

    [Fact] public void Visual_filters_are_anded_with_entire_or_expression()
    {
        var request = new SearchRequest { Text = "产品 | 报价", Kind = SearchKind.Images, MinimumSize = 1000 };
        var result = SearchRules.Filter(Files, request, "test");
        Assert.Equal("产品 主图.png", Assert.Single(result.Items).Name);
        Assert.StartsWith("<\"产品\" | \"报价\">", SearchRules.Compile(request).EverythingExpression);
    }
    [Fact] public void Sort_happens_before_paging()
    {
        var request = new SearchRequest { Kind = SearchKind.Files, Sort = SearchSort.Size, Descending = true, PageSize = 1, Offset = 1 };
        var result = SearchRules.Filter(Files, request, "test");
        Assert.Equal(4, result.Total);
        Assert.Equal("报价.PDF", Assert.Single(result.Items).Name);
    }
    [Fact] public void Matching_path_is_explicit()
    {
        Assert.Empty(SearchRules.Filter(Files, new() { Text = "备份" }, "test").Items);
        Assert.Single(SearchRules.Filter(Files, new() { Text = "备份", MatchPath = true }, "test").Items);
    }
    [Fact] public void Folder_and_unknown_size_do_not_match_zero_size()
        => Assert.Empty(SearchRules.Filter(Files, new() { MaximumSize = 0 }, "test").Items);
    [Fact] public void Cancellation_does_not_return_partial_success()
        => Assert.Throws<OperationCanceledException>(() => SearchRules.Filter(Files, new(), "test", cancellationToken: new(true)));
}
