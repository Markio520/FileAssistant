using System.Diagnostics;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Organize;

namespace FileAssistant.Infrastructure.FileSystem;

internal static class CopyScanner
{
    public static IReadOnlyList<CopySource> Scan(CopyRequest request, IProgress<ProcessingProgress>? progress, CancellationToken token)
    {
        var destination = CopyPlanner.Normalize(request.Destination);
        CopyFileSystem.CheckPath(destination);
        var sources = new List<CopySource>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var directories = new List<string>();
        var clock = Stopwatch.StartNew();
        var last = 0L;
        foreach (var raw in request.Paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Length).ThenBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            string path;
            try { path = CopyPlanner.Normalize(raw); }
            catch (ArgumentException error) { sources.Add(new(raw, "", "", false, null, error.Message)); continue; }
            if (directories.Any(root => CopyPlanner.Within(path, root))) continue;
            FileAttributes? attributes;
            try { attributes = CopyFileSystem.Attributes(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { sources.Add(new(path, "", "", false, null, CopyFileSystem.Friendly(error))); continue; }
            var isDirectory = attributes is { } a && (a & FileAttributes.Directory) != 0;
            if (isDirectory && CopyPlanner.Within(destination, path)) throw new IOException("目标目录位于待复制源目录内部或与源目录相同，请调整目标位置。");
            if (isDirectory) directories.Add(path);
            Visit(path, isDirectory ? path : "", "");
        }
        progress?.Report(new(sources.Count, sources.Count, $"扫描完成：{sources.Count:N0} 项"));
        return sources;

        void Visit(string path, string root, string relative)
        {
            token.ThrowIfCancellationRequested();
            if (!known.Add(path)) return;
            var folder = false;
            try
            {
                var attributes = CopyFileSystem.Attributes(path) ?? throw new FileNotFoundException("源文件已不存在。", path);
                folder = (attributes & FileAttributes.Directory) != 0;
                CopyFileSystem.CheckPath(path);
                if (folder)
                {
                    sources.Add(new(path, root, relative, true, null));
                    // Enumeration errors leave a visible rejected entry, never a silently incomplete scan.
                    foreach (var child in Directory.EnumerateFileSystemEntries(path).Select(entry => { token.ThrowIfCancellationRequested(); return entry; }).Order(StringComparer.OrdinalIgnoreCase))
                        Visit(child, root, Path.GetRelativePath(root, child));
                }
                else
                {
                    var stamp = CopyFileSystem.Stamp(path, false, token);
                    if (identities.Add(stamp.Identity)) sources.Add(new(path, root, relative, false, stamp));
                }
                if (clock.ElapsedMilliseconds - last >= 100)
                { last = clock.ElapsedMilliseconds; progress?.Report(new(sources.Count, 0, $"正在扫描：{sources.Count:N0} 项 · {path}")); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { sources.Add(new(path, root, relative, folder, null, CopyFileSystem.Friendly(error))); }
        }
    }
}
