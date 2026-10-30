using FileAssistant.Core.Features.FileInput;
using FileAssistant.Infrastructure.FileSystem;

namespace FileAssistant.Infrastructure.Tests;

public sealed class FileInputReaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-input-tests", Guid.NewGuid().ToString("N"));
    private readonly FileInputReader reader = new();

    public FileInputReaderTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task Reads_quoted_unicode_paths_and_metadata_without_modifying_the_original()
    {
        var path = Path.Combine(root, "文件 空格.txt");
        await File.WriteAllTextAsync(path, "file input fixture");
        var before = new FileInfo(path);
        var modified = before.LastWriteTimeUtc;
        var report = await reader.ReadAsync([$"  \"{path}\"  "], CancellationToken.None);
        var item = Assert.Single(report.Items);
        Assert.Equal(FileImportStatus.Ready, item.Status);
        Assert.Equal(path, item.File!.FullPath);
        Assert.Equal(before.Length, item.File.Size);
        Assert.Equal(modified, item.File.Modified!.Value.UtcDateTime);
        Assert.Equal("file input fixture", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Normalizes_paths_and_reports_case_insensitive_duplicates()
    {
        var path = Path.Combine(root, "sample.txt");
        await File.WriteAllTextAsync(path, "sample");
        var relativeSegments = Path.Combine(root, "unused", "..", "sample.txt");
        var report = await reader.ReadAsync([path, path.ToUpperInvariant(), relativeSegments], CancellationToken.None);
        Assert.Equal([FileImportStatus.Ready, FileImportStatus.Duplicate, FileImportStatus.Duplicate], report.Items.Select(item => item.Status));
    }

    [Fact]
    public async Task Folder_is_one_reference_without_expanding_contents()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "child.txt"), "child");
        var report = await reader.ReadAsync([root + Path.DirectorySeparatorChar], CancellationToken.None);
        var item = Assert.Single(report.Items);
        Assert.Equal(FileImportStatus.Ready, item.Status);
        Assert.True(item.File!.IsFolder);
        Assert.Null(item.File.Size);
        Assert.Equal(root, item.File.FullPath);
        Assert.Contains("不展开", item.Reason);
    }

    [Theory]
    [InlineData("relative.txt")]
    [InlineData("https://example.com/file.txt")]
    [InlineData("\\\\.\\PhysicalDrive0")]
    [InlineData("<无本地路径：虚拟文件>")]
    public async Task Reports_unsupported_input_instead_of_guessing_a_location(string path)
    {
        var report = await reader.ReadAsync([path], CancellationToken.None);
        var item = Assert.Single(report.Items);
        Assert.Equal(FileImportStatus.Unsupported, item.Status);
        Assert.NotEmpty(item.Reason);
        Assert.Null(item.File);
    }

    [Fact]
    public async Task Missing_file_does_not_discard_other_valid_items()
    {
        var path = Path.Combine(root, "available.txt");
        await File.WriteAllTextAsync(path, "available");
        var report = await reader.ReadAsync([Path.Combine(root, "missing.txt"), path], CancellationToken.None);
        Assert.Equal([FileImportStatus.Unavailable, FileImportStatus.Ready], report.Items.Select(item => item.Status));
    }

    [Fact]
    public async Task Cancelled_request_never_returns_a_partial_report()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync([root], cancellation.Token));
    }

    public void Dispose()
    {
        // Only this test's uniquely created directory is removed.
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
