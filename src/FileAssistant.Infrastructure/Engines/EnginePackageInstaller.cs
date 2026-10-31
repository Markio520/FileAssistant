using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FileAssistant.Core.Engines;

namespace FileAssistant.Infrastructure.Engines;

/// <summary>Explicit downloads only; verified archives are installed beside the application, never over running DLLs.</summary>
public sealed class EnginePackageInstaller(HttpClient client, string enginesDirectory) : IEnginePackageInstaller
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private const long MaximumArchiveSize = 512L * 1024 * 1024;
    private const long MaximumExtractedSize = 2L * 1024 * 1024 * 1024;

    public async Task<string> InstallAsync(EnginePackage package, IProgress<int>? progress, CancellationToken token)
    {
        if (package.DownloadUri.Scheme != Uri.UriSchemeHttps || !Regex.IsMatch(package.Id, "^[a-z0-9-]+$") ||
            !Regex.IsMatch(package.Version, "^[a-zA-Z0-9][a-zA-Z0-9.+-]*$") || !Regex.IsMatch(package.Sha256, "^[a-fA-F0-9]{64}$"))
            throw new FileProcessingException("引擎下载清单无效。");
        await gate.WaitAsync(token);
        var staging = Path.Combine(enginesDirectory, ".installing-" + Guid.NewGuid().ToString("N"));
        var archivePath = staging + (package.ArchiveKind == EngineArchiveKind.WindowsInstaller ? ".msi" : ".zip");
        try
        {
            Directory.CreateDirectory(staging);
            using var response = await client.GetAsync(package.DownloadUri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            if (length > MaximumArchiveSize) throw new FileProcessingException("引擎下载包超过大小限制。");
            await using (var network = await response.Content.ReadAsStreamAsync(token))
            await using (var archive = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81_920]; long received = 0; int count; var lastProgress = -1;
                while ((count = await network.ReadAsync(buffer, token)) > 0)
                {
                    received += count;
                    if (received > MaximumArchiveSize) throw new FileProcessingException("引擎下载包超过大小限制。");
                    hash.AppendData(buffer, 0, count); await archive.WriteAsync(buffer.AsMemory(0, count), token);
                    var percent = length > 0 ? (int)Math.Min(89, received * 90 / length.Value) : 0;
                    if (percent != lastProgress) { progress?.Report(percent); lastProgress = percent; }
                }
                if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), System.Convert.FromHexString(package.Sha256)))
                    throw new FileProcessingException("引擎校验失败，下载未安装，请重试。");
            }
            if (package.ArchiveKind == EngineArchiveKind.WindowsInstaller)
            {
                progress?.Report(90);
                // Administrative extraction creates a private runtime layout; it does not register/install Office globally.
                // Let Windows Installer finish before cleaning up on cancel, so its service cannot keep writing into staging.
                await LocalEngineProcess.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"),
                    ["/a", archivePath, "/qn", "/norestart", "TARGETDIR=" + Path.GetFullPath(staging)], TimeSpan.FromMinutes(15), CancellationToken.None,
                    "无法解包文档引擎，请检查磁盘空间或稍后重试。");
                token.ThrowIfCancellationRequested();
                var copiedInstaller = Path.Combine(staging, Path.GetFileName(archivePath));
                if (File.Exists(copiedInstaller)) File.Delete(copiedInstaller);
            }
            else if (package.ArchiveKind == EngineArchiveKind.Zip)
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                if (zip.Entries.Count > 30_000 || zip.Entries.Sum(entry => entry.Length) > MaximumExtractedSize)
                    throw new FileProcessingException("引擎解压大小超过限制。");
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    var name = entry.FullName.Replace('\\', '/');
                    if (!name.StartsWith(package.ArchiveRoot + "/", StringComparison.Ordinal))
                        throw new FileProcessingException("引擎包目录与下载清单不符。");
                    var relative = name[(package.ArchiveRoot.Length + 1)..];
                    var path = ChildPath(staging, relative);
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new FileProcessingException("引擎包包含不支持的链接。");
                    if (name.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
                    // Headers, import libraries and the player are not runtime dependencies.
                    if (relative.StartsWith("include/", StringComparison.Ordinal) || relative.StartsWith("lib/", StringComparison.Ordinal) || relative == "bin/ffplay.exe") continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using var input = entry.Open();
                    await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await input.CopyToAsync(output, token);
                }
            }
            else throw new FileProcessingException("不支持此引擎包类型。");
            if (!File.Exists(ChildPath(staging, package.Executable))) throw new FileProcessingException("引擎包缺少执行组件。");
            progress?.Report(95); token.ThrowIfCancellationRequested();
            var relativeDirectory = Path.Combine(package.Id, package.Version, Guid.NewGuid().ToString("N"));
            var destination = ChildPath(enginesDirectory, relativeDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            // A single atomic pointer switches future calls; completed/active engine files are never overwritten.
            var pointer = Path.Combine(enginesDirectory, package.Id + ".json");
            var temporaryPointer = pointer + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPointer, JsonSerializer.Serialize(new Installation(package.Version, package.Sha256, relativeDirectory)), CancellationToken.None);
                File.Move(temporaryPointer, pointer, overwrite: true);
            }
            finally { if (File.Exists(temporaryPointer)) File.Delete(temporaryPointer); }
            progress?.Report(100);
            return ChildPath(destination, package.Executable);
        }
        catch (OperationCanceledException) { throw; }
        catch (FileProcessingException) { throw; }
        catch (Exception error) { throw new FileProcessingException("下载或安装引擎失败，请检查网络、磁盘空间和目录权限后重试。", error); }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); if (File.Exists(archivePath)) File.Delete(archivePath); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            gate.Release();
        }
    }

    public static string? FindInstalled(string directory, EnginePackage package)
    {
        try
        {
            var installation = JsonSerializer.Deserialize<Installation>(File.ReadAllText(Path.Combine(directory, package.Id + ".json")));
            if (installation is null || installation.Version != package.Version || installation.Sha256 != package.Sha256) return null;
            var executable = ChildPath(ChildPath(directory, installation.Directory), package.Executable);
            return File.Exists(executable) ? executable : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or FileProcessingException or ArgumentException) { return null; }
    }
    private static string ChildPath(string directory, string relative)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(root, relative));
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase) || relative.Contains(':'))
            throw new FileProcessingException("引擎包包含无效的文件路径。");
        return result;
    }
    private sealed record Installation(string Version, string Sha256, string Directory);
}
