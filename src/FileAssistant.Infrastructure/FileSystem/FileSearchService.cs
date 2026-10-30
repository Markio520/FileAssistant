using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.Search;
using FileAssistant.Infrastructure.Windows;

namespace FileAssistant.Infrastructure.FileSystem;

public sealed class FileSearchService(EverythingSearchClient everything, DirectorySearchIndex directories, EverythingRuntime? runtime = null) : IFileSearch
{
    public Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch,
        IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        _ = SearchRules.Compile(request); // Fail consistently before accessing any search source.
        return request.Scope switch
        {
            SearchScope.Everything => SearchIndexAsync(request, progress, cancellationToken),
            SearchScope.Folder => directories.SearchAsync(request, progress, cancellationToken),
            _ => Task.Run(() => SearchRules.Filter(batch, request, "当前操作批次", cancellationToken: cancellationToken), cancellationToken)
        };
    }

    public void InvalidateFolder() => directories.Invalidate();
    public void RetryIndexInitialization() => runtime?.RetryInitialization();

    private async Task<SearchPage> SearchIndexAsync(SearchRequest request, IProgress<SearchProgress>? progress, CancellationToken token)
    {
        if (runtime is not null) await runtime.EnsureReadyAsync(progress, token);
        return await everything.SearchAsync(request, token);
    }
}
