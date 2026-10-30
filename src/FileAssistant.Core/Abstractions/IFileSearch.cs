using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Abstractions;

public interface IFileSearch
{
    Task<SearchPage> SearchAsync(SearchRequest request, IReadOnlyList<SearchFile> batch,
        IProgress<SearchProgress>? progress, CancellationToken cancellationToken);
    void InvalidateFolder();
    void RetryIndexInitialization() { }
}
