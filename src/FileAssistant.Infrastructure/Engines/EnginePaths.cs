namespace FileAssistant.Infrastructure.Engines;

public sealed record EnginePaths(string BundledFfmpegExecutable, string CacheDirectory, string? ManagedEnginesDirectory = null, string? OfficeExecutableOverride = null)
{
    public string? OfficeExecutable => OfficeExecutableOverride ?? (ManagedEnginesDirectory is { } directory
        ? EnginePackageInstaller.FindInstalled(directory, EnginePackageCatalog.LibreOffice) : null);
    public string FfmpegExecutable => ManagedEnginesDirectory is { } directory
        ? EnginePackageInstaller.FindInstalled(directory, EnginePackageCatalog.Ffmpeg) ?? BundledFfmpegExecutable : BundledFfmpegExecutable;
    public static EnginePaths Installed { get; } = new(
        Path.Combine(AppContext.BaseDirectory, "Engines", "ffmpeg", "bin", "ffmpeg.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileAssistant", "Previews"), ExtensionsDirectory);
    public static string ExtensionsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileAssistant", "Engines");
}
