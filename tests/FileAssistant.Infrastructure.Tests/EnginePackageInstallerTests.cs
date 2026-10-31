using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FileAssistant.Core.Engines;
using FileAssistant.Infrastructure.Engines;

namespace FileAssistant.Infrastructure.Tests;

public sealed class EnginePackageInstallerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FileAssistant-package-test-" + Guid.NewGuid().ToString("N"));
    private static byte[] Archive(string entry)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        { using var stream = zip.CreateEntry("bundle/" + entry).Open(); stream.Write(Encoding.UTF8.GetBytes("test engine data, never executed")); }
        return buffer.ToArray();
    }
    private static EnginePackage Package(byte[] data) => new("test", "Test engine", "1.0", new("https://example.invalid/engine.zip"),
        System.Convert.ToHexString(SHA256.HashData(data)), "bundle", "bin/engine.exe");

    [Fact]
    public async Task Verified_package_installs_atomically_and_repair_preserves_previous_version()
    {
        var data = Archive("bin/engine.exe"); var package = Package(data);
        using var client = new HttpClient(new PackageHandler(data));
        var installer = new EnginePackageInstaller(client, root);
        Assert.Null(EnginePackageInstaller.FindInstalled(root, package));
        var first = await installer.InstallAsync(package, null, default);
        Assert.Equal(first, EnginePackageInstaller.FindInstalled(root, package));
        var second = await installer.InstallAsync(package, null, default);
        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first)); Assert.True(File.Exists(second));
        Assert.Equal(second, EnginePackageInstaller.FindInstalled(root, package));
        Assert.Empty(Directory.GetFiles(root, "*.zip"));
        Assert.Empty(Directory.GetDirectories(root, ".installing-*"));
    }
    [Fact]
    public async Task Failed_hash_does_not_activate_or_leave_downloaded_data()
    {
        var data = Archive("bin/engine.exe"); var package = Package(data) with { Sha256 = new string('0', 64) };
        using var client = new HttpClient(new PackageHandler(data));
        var installer = new EnginePackageInstaller(client, root);
        await Assert.ThrowsAsync<FileProcessingException>(() => installer.InstallAsync(package, null, default));
        Assert.Null(EnginePackageInstaller.FindInstalled(root, package));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }
    [Fact]
    public async Task Archive_cannot_escape_its_own_installation_directory()
    {
        var data = Archive("../../outside.txt");
        using var client = new HttpClient(new PackageHandler(data));
        var installer = new EnginePackageInstaller(client, root);
        await Assert.ThrowsAsync<FileProcessingException>(() => installer.InstallAsync(Package(data), null, default));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }
    [Fact]
    public async Task Cancellation_leaves_the_current_engine_usable()
    {
        var data = Archive("bin/engine.exe"); var package = Package(data);
        using var client = new HttpClient(new PackageHandler(data));
        var installer = new EnginePackageInstaller(client, root);
        var installed = await installer.InstallAsync(package, null, default);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installer.InstallAsync(package,
            new InlineProgress(value => { if (value > 0) cancellation.Cancel(); }), cancellation.Token));
        Assert.Equal(installed, EnginePackageInstaller.FindInstalled(root, package));
        Assert.True(File.Exists(installed));
        Assert.Empty(Directory.GetFiles(root, "*.zip"));
        Assert.Empty(Directory.GetDirectories(root, ".installing-*"));
    }

    [Fact]
    public async Task Real_ffmpeg_archive_layout_and_runtime_files_install_from_the_verified_catalogue()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "FileAssistant.slnx"))) repository = repository.Parent;
        var package = EnginePackageCatalog.Ffmpeg;
        var cache = Path.Combine(repository!.FullName, "artifacts", "dependencies", "ffmpeg", package.Version, "x64");
        var archive = Path.Combine(cache, Path.GetFileName(package.DownloadUri.LocalPath));
        using var client = new HttpClient(new ArchiveHandler(archive));
        var installer = new EnginePackageInstaller(client, root);
        var installed = await installer.InstallAsync(package, null, default);
        Assert.Equal(installed, EnginePackageInstaller.FindInstalled(root, package));
        Assert.Equal(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(cache, "bundle", "bin", "ffmpeg.exe"))),
            SHA256.HashData(await File.ReadAllBytesAsync(installed)));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(installed)!, "avcodec-62.dll")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(installed)!, "..", "LICENSE.txt")));
    }
    private sealed class InlineProgress(Action<int> report) : IProgress<int> { public void Report(int value) => report(value); }
    private sealed class ArchiveHandler(string path) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(path)) });
    }
    private sealed class PackageHandler(byte[] data) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
