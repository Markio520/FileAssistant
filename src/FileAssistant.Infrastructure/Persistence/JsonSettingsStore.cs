using System.Text;
using FileAssistant.Core.Abstractions;

namespace FileAssistant.Infrastructure.Persistence;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    public async Task<string?> ReadAsync()
    {
        try { return await File.ReadAllTextAsync(path).ConfigureAwait(false); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task WriteAsync(string contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, contents, new UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
