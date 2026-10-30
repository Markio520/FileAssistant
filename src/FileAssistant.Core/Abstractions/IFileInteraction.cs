using FileAssistant.Core.Features.Search;

namespace FileAssistant.Core.Abstractions;

public interface IFileInteraction
{
    void Open(SearchFile file);
    void Reveal(SearchFile file);
    void CopyText(string text);
}

public sealed class FileInteractionException(string message) : Exception(message);
