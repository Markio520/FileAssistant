using FileAssistant.Core.Abstractions;
using FileAssistant.Core.Features.FileInput;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.Infrastructure.FileSystem;

/// <summary>Read references and metadata only; never expand a folder or modify its contents.</summary>
public sealed class FileInputReader : IFileInputReader
{
    public Task<FileImportReport> ReadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        => Task.Run(() => Read(paths, cancellationToken), cancellationToken);

    private static FileImportReport Read(IReadOnlyList<string> paths, CancellationToken token)
    {
        var items = new List<FileImportItem>(paths.Count);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplied in paths)
        {
            token.ThrowIfCancellationRequested();
            var path = supplied.Trim();
            if (path.Length >= 2 && path[0] == '"' && path[^1] == '"') path = path[1..^1];
            try
            {
                if (!Path.IsPathFullyQualified(path))
                {
                    items.Add(new(supplied, FileImportStatus.Unsupported, "请提供文件或文件夹的完整路径。"));
                    continue;
                }
                if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
                {
                    items.Add(new(supplied, FileImportStatus.Unsupported, "不支持网页地址或设备路径，请添加实际文件。"));
                    continue;
                }
                path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                if (!known.Add(path))
                {
                    items.Add(new(path, FileImportStatus.Duplicate, "本次输入中已包含此路径。"));
                    continue;
                }
                var attributes = File.GetAttributes(path);
                var folder = (attributes & FileAttributes.Directory) != 0;
                FileSystemInfo info = folder ? new DirectoryInfo(path) : new FileInfo(path);
                var file = new SearchFile(path, folder, folder ? null : ((FileInfo)info).Length, info.LastWriteTimeUtc);
                items.Add(new(path, FileImportStatus.Ready, folder ? "按文件夹本身加入，不展开内部文件。" : "文件可用。", file));
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            { items.Add(new(path, FileImportStatus.Unavailable, "项目已移动或删除，请检查路径。")); }
            catch (UnauthorizedAccessException)
            { items.Add(new(path, FileImportStatus.Unavailable, "没有读取此项目的权限。")); }
            catch (Exception e) when (e is IOException or System.Security.SecurityException)
            { items.Add(new(path, FileImportStatus.Unavailable, "无法读取此项目，请检查连接、占用或访问权限。")); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            { items.Add(new(supplied, FileImportStatus.Unsupported, "路径格式无效，请使用完整文件路径。")); }
        }
        token.ThrowIfCancellationRequested();
        return new(items);
    }
}
