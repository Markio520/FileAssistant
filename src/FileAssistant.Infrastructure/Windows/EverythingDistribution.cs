using System.Security.Cryptography;
using System.Security.Principal;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.Infrastructure.Windows;

public static class EverythingDistribution
{
    public const string Version = "1.4.1.1032";
    public const string ExecutableHash = "F191F756996A14A11E5445FA7103D302EFD510CF2FBF920E6C0C8ED51D512E36";
    public const string LicenseHash = "C13D19ADCBFD5D07E9512DE9DF99956A3423399ED1FADC5FD33186697AD8DF2F";
    public const string SetupArgument = "--setup-search-service";
    public static string BundledExecutable => Path.Combine(AppContext.BaseDirectory, "SearchEngine", "Everything.exe");
    public static string InstalledExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FileAssistant", "SearchEngine", Version, "Everything.exe");
    public static string UserInstance => "FileAssistant-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(WindowsIdentity.GetCurrent().User!.Value)))[..12];
    public static string ServiceName(string instance) => $"Everything ({instance})";
    public static string ServicePipe(string instance) => @"\\.\PIPE\" + instance + ".SearchService";

    public static void VerifyExecutable(string filename)
    {
        if (!File.Exists(filename)) throw new SearchException("内置搜索组件缺失。请重新安装本软件，或暂用“指定目录”搜索。");
        using var file = File.OpenRead(filename);
        if (!Convert.ToHexString(SHA256.HashData(file)).Equals(ExecutableHash, StringComparison.Ordinal))
            throw new SearchException("内置搜索组件校验失败。请重新安装本软件后重试。");
    }

    internal static byte[] ReadVerifiedBytes(string filename, string hash)
    {
        var bytes = File.ReadAllBytes(filename);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.Ordinal)) throw new InvalidDataException("Bundled component checksum mismatch.");
        return bytes;
    }
}
