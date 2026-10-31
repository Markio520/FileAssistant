using System.Text.Json;
using FileAssistant.Core.Features.FileCollections;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.Infrastructure.Persistence;

public sealed class JsonFileCollectionStore(string path) : IFileCollectionStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public async Task<CollectionLoadResult> LoadAsync(CancellationToken token)
    {
        FileCollectionSnapshot? snapshot;
        try
        {
            var text = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
            snapshot = JsonSerializer.Deserialize<FileCollectionSnapshot>(text, Options) ?? throw new JsonException();
            if (snapshot.Version != 1) return new(null, "保存的文件夹来自其他版本，现有记录已保留，本次暂停自动保存。", false);
            snapshot = FileCollection.Restore(snapshot).Snapshot(snapshot.CurrentFolderId);
        }
        catch (FileNotFoundException) { return new(null); }
        catch (DirectoryNotFoundException) { return new(null); }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        {
            try
            {
                File.Copy(path, path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".bak", overwrite: false);
                return new(null, "保存的文件夹数据损坏，已保留备份，可继续建立新的文件夹。");
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            { return new(null, "文件夹数据读取失败，原记录已保留，本次暂停自动保存。", false); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return new(null, "无法读取保存的文件夹，原记录已保留，本次暂停自动保存。", false); }
        return await Task.Run(() =>
        {
            var updates = new Dictionary<string, (SearchFile, string?)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in snapshot.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (updates.ContainsKey(entry.File.FullPath)) continue;
                try
                {
                    FileSystemInfo item = entry.File.IsFolder ? new DirectoryInfo(entry.File.FullPath) : new FileInfo(entry.File.FullPath);
                    item.Refresh();
                    if (!item.Exists) updates.Add(entry.File.FullPath, (entry.File, "失效：文件已移动、删除或当前无法访问。"));
                    else updates.Add(entry.File.FullPath, (new(entry.File.FullPath, item is DirectoryInfo,
                        item is FileInfo file ? file.Length : null, item.LastWriteTimeUtc), null));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { updates.Add(entry.File.FullPath, (entry.File, "失效：当前无法访问此路径。")); }
            }
            var collection = FileCollection.Restore(snapshot); collection.UpdateReferences(updates);
            var missing = updates.Values.Count(update => update.Item2 is not null);
            return new CollectionLoadResult(collection.Snapshot(snapshot.CurrentFolderId), missing > 0 ? $"已恢复文件夹；{missing:N0} 个路径失效，引用保留。" : null);
        }, token).ConfigureAwait(false);
    }
    public async Task SaveAsync(FileCollectionSnapshot snapshot, CancellationToken token)
    {
        FileCollection.Restore(snapshot);
        await gate.WaitAsync(token).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { await JsonSerializer.SerializeAsync(output, snapshot, Options, token).ConfigureAwait(false); await output.FlushAsync(token).ConfigureAwait(false); }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }
}
