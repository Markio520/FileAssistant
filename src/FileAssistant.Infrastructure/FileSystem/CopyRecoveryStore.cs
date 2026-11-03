using System.Text.Json;
using FileAssistant.Core.Features.Organize;

namespace FileAssistant.Infrastructure.FileSystem;

internal sealed class CopyRecoveryStore(string directory)
{
    public void Save(CopyRecovery recovery)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, recovery.Id.ToString("N") + ".json");
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(stream, recovery); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public IReadOnlyList<CopyRecovery> ReadAll()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json").Select(path => Read(Guid.Parse(Path.GetFileNameWithoutExtension(path))))
            .Where(x => x.State is not ("已恢复" or "已删除备份")).OrderByDescending(x => x.Created).ToArray();
    }
    public CopyRecovery Read(Guid id)
    {
        var path = Path.Combine(directory, id.ToString("N") + ".json");
        var record = JsonSerializer.Deserialize<CopyRecovery>(File.ReadAllText(path)) ?? throw new IOException("恢复记录为空。");
        if (record.Id != id || !CopyPlanner.Equal(record.Backup, BackupPath(record.Target, id))) throw new IOException("恢复记录路径校验失败，未操作磁盘文件。");
        return record;
    }
    public static string BackupPath(string target, Guid id) => Path.Combine(Path.GetDirectoryName(target)!, $".fileassistant-backup-{id:N}.bak");
    private static string RestorePath(CopyRecovery record) => Path.Combine(Path.GetDirectoryName(record.Target)!, $".fileassistant-restore-{record.Id:N}.bak");

    public void Restore(Guid id, CancellationToken token)
    {
        var record = Read(id);
        if (record.State is "已恢复" or "已删除备份") return;
        if (record.State == "正在删除备份") throw new IOException("这份备份已开始删除，请继续删除操作。");
        CopyFileSystem.CheckPath(record.Target); CopyFileSystem.CheckPath(record.Backup);
        var displaced = RestorePath(record);
        // Reconcile a crash after the old backup was already restored.
        if (!File.Exists(record.Backup))
        {
            if (File.Exists(record.Target) && CopyFileSystem.Stamp(record.Target, true, token).Hash == record.OldHash)
            { FinishRestore(record, displaced, token); return; }
            throw new IOException("备份尚不可用，请连接原目标设备并检查记录。");
        }
        using var backup = WindowsCopyFile.LockForRename(record.Backup);
        if (CopyFileSystem.Hash(backup, token) != record.OldHash) throw new IOException("备份内容已变化，未执行恢复。");
        FileStream? current = null;
        try
        {
            if (CopyFileSystem.Exists(record.Target))
            {
                current = WindowsCopyFile.LockForRename(record.Target);
                if (CopyFileSystem.Hash(current, token) != record.NewHash) throw new IOException("当前目标已被修改，未覆盖它。请先另存当前文件后再恢复。");
                if (File.Exists(displaced)) throw new IOException("存在上次恢复的保留文件，请先检查恢复记录。");
            }
            token.ThrowIfCancellationRequested();
            Save(record with { State = "正在恢复" });
            if (current is not null) WindowsCopyFile.RenameLocked(current, displaced);
            try { WindowsCopyFile.RenameLocked(backup, record.Target); }
            catch
            {
                if (current is not null && !CopyFileSystem.Exists(record.Target)) WindowsCopyFile.RenameLocked(current, record.Target);
                throw;
            }
        }
        finally { current?.Dispose(); }
        FinishRestore(record, displaced, CancellationToken.None);
    }
    private void FinishRestore(CopyRecovery record, string displaced, CancellationToken token)
    {
        if (File.Exists(displaced))
        {
            CopyFileSystem.CheckPath(displaced);
            if (CopyFileSystem.Stamp(displaced, true, token).Hash != record.NewHash) throw new IOException("恢复前保留文件已变化，已保留供检查。");
            CopyFileSystem.DeleteOwnedTemporary(displaced);
        }
        Save(record with { State = "已恢复", Message = null });
    }
    public void Discard(Guid id, CancellationToken token)
    {
        var record = Read(id);
        if (!Directory.Exists(Path.GetDirectoryName(record.Target))) throw new IOException("目标目录不可用，请连接原设备后再删除备份。");
        var removal = RestorePath(record);
        if (File.Exists(removal))
        {
            if (record.State != "正在删除备份") throw new IOException("恢复尚未完成，请先完成恢复。");
            CopyFileSystem.CheckPath(removal);
            if (CopyFileSystem.Stamp(removal, true, token).Hash != record.OldHash) throw new IOException("待删除备份已变化，未删除。");
            CopyFileSystem.DeleteOwnedTemporary(removal);
        }
        if (File.Exists(record.Backup))
        {
            CopyFileSystem.CheckPath(record.Backup);
            using (var locked = WindowsCopyFile.LockForRename(record.Backup))
            {
                if (CopyFileSystem.Hash(locked, token) != record.OldHash) throw new IOException("备份内容已变化，未删除。");
                token.ThrowIfCancellationRequested();
                Save(record with { State = "正在删除备份" });
                // Move under the held handle first; deleting then cannot affect a substituted path.
                WindowsCopyFile.RenameLocked(locked, RestorePath(record));
            }
            CopyFileSystem.DeleteOwnedTemporary(RestorePath(record));
        }
        Save(record with { State = "已删除备份", Message = null });
    }
}
