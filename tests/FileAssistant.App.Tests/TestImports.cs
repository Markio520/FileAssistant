using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Tests;

internal static class TestImports
{
    public static Task<FileImportReport> None(IReadOnlyList<SearchFile> files) => Task.FromResult(FileImportReport.Empty);
    public static Task<FileImportReport> Added(IEnumerable<SearchFile> files) => Task.FromResult(new FileImportReport(
        files.Select(file => new FileImportItem(file.FullPath, FileImportStatus.Added, "文件可用。", file)).ToArray()));
}
