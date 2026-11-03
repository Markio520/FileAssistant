using System.Security.Cryptography;
using FileAssistant.Core.Features.Organize;

namespace FileAssistant.Infrastructure.FileSystem;

internal static class CopyFileSystem
{
    public static bool Exists(string path) => Attributes(path) is not null;
    public static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    public static void CheckPath(string path)
    {
        CopyPlanner.Normalize(path);
        for (string? part = path; part is not null; part = Path.GetDirectoryName(part))
            if (Attributes(part) is { } attributes && (attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("首版不跟随目录链接或重解析点，请选择原始磁盘路径。");
    }
    public static FileStamp Stamp(string path, bool hash, CancellationToken token)
    {
        CheckPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var stamp = WindowsCopyFile.Stamp(stream);
        return hash ? stamp with { Hash = Hash(stream, token) } : stamp;
    }
    public static string Hash(FileStream stream, CancellationToken token)
    {
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        int read;
        while ((read = stream.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        token.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static CopyTarget? Target(string path, bool hash, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            CheckPath(path);
            var attributes = Attributes(path);
            if (attributes is null) return null;
            return (attributes & FileAttributes.Directory) != 0 ? new(true, null) : new(false, Stamp(path, hash, token));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { return new(false, null, error.Message); }
    }
    public static bool Same(FileStamp? expected, FileStamp actual) => expected is not null && expected.Length == actual.Length &&
        expected.ModifiedUtcTicks == actual.ModifiedUtcTicks && expected.Identity == actual.Identity && (expected.Hash is null || expected.Hash == actual.Hash);
    public static bool Fatal(Exception error) => (error.HResult & 0xffff) is 39 or 112 or 21 or 53 or 55 or 64 or 67 or 1167 or 1231;
    public static string Friendly(Exception error) => (error.HResult & 0xffff) switch
    {
        39 or 112 => "目标空间不足，已停止剩余项目。", 21 or 53 or 55 or 64 or 67 or 1167 or 1231 => "目标设备或网络连接不可用，已停止剩余项目。",
        32 or 33 => "文件被占用，请关闭使用它的程序后重试。", 5 => "没有访问权限，请检查目录和文件权限。", _ => error.Message
    };
    public static void DeleteOwnedTemporary(string path)
    {
        if (!File.Exists(path)) return;
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }
}
