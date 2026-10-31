namespace FileAssistant.Core.Engines;

/// <summary>Only application-owned catalogue entries may be offered as downloads.</summary>
public enum EngineArchiveKind { Zip, WindowsInstaller }
public sealed record EnginePackage(string Id, string Name, string Version, Uri DownloadUri, string Sha256,
    string ArchiveRoot, string Executable, long DownloadBytes = 0, string License = "见随包声明", EngineArchiveKind ArchiveKind = EngineArchiveKind.Zip);
public interface IEnginePackageInstaller
{
    Task<string> InstallAsync(EnginePackage package, IProgress<int>? progress, CancellationToken token);
}
