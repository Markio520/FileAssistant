using FileAssistant.Core.Features.Search;

namespace FileAssistant.Infrastructure.FileSystem;

/// <summary>One explicit folder snapshot. Refresh invalidates it; no silent background disk scan.</summary>
public sealed class DirectorySearchIndex
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Snapshot? snapshot;
    private int generation;

    public void Invalidate() { Interlocked.Increment(ref generation); snapshot = null; }

    public async Task<SearchPage> SearchAsync(SearchRequest request, IProgress<SearchProgress>? progress, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Folder)) throw new SearchException("先选择一个文件夹，再开始搜索。");
        string root;
        try { root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Folder)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new SearchException("文件夹路径无效，请重新选择。"); }
        await gate.WaitAsync(token);
        try
        {
            var currentGeneration = generation;
            var current = snapshot;
            if (current is null || !string.Equals(current.Root, root, StringComparison.OrdinalIgnoreCase) || current.Recursive != request.IncludeSubfolders)
            {
                current = await Task.Run(() => Scan(root, request.IncludeSubfolders, progress, token), token);
                token.ThrowIfCancellationRequested();
                if (currentGeneration == generation) snapshot = current;
            }
            return await Task.Run(() => SearchRules.Filter(current.Files, request, "目录快照 · 刷新可重新扫描", current.Skipped, token), token);
        }
        finally { gate.Release(); }
    }

    private static Snapshot Scan(string root, bool recursive, IProgress<SearchProgress>? progress, CancellationToken token)
    {
        if (!Directory.Exists(root)) throw new SearchException("文件夹不存在或无法访问，请检查路径与访问权限。");
        var files = new List<SearchFile>();
        var pending = new Stack<string>();
        pending.Push(root);
        var skipped = 0;
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", new EnumerationOptions
                { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false, ReturnSpecialDirectories = false }))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var folder = (entry.Attributes & FileAttributes.Directory) != 0;
                        files.Add(new(entry.FullName, folder, folder ? null : ((FileInfo)entry).Length, entry.LastWriteTimeUtc));
                        // Do not follow junctions/symlinks into cycles or outside the selected tree.
                        if (folder && recursive && (entry.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(entry.FullName);
                        if (files.Count % 256 == 0) progress?.Report(new(files.Count, root));
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped++; }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (directory == root) throw new SearchException("无法读取此文件夹，请检查访问权限或连接状态。");
                skipped++;
            }
        }
        progress?.Report(new(files.Count, root));
        return new(root, recursive, files, skipped);
    }

    private sealed record Snapshot(string Root, bool Recursive, IReadOnlyList<SearchFile> Files, int Skipped);
}
