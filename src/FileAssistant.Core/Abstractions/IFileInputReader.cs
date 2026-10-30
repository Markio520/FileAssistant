using FileAssistant.Core.Features.FileInput;

namespace FileAssistant.Core.Abstractions;

public interface IFileInputReader
{
    Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);
}
