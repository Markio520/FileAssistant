using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.Infrastructure.Windows;

/// <summary>Installs only our named helper, using a pinned executable in protected Program Files.</summary>
public static class EverythingServiceSetup
{
    public static async Task EnsureRunningAsync(string instance, CancellationToken token)
    {
        if (IsRunning(instance))
        {
            EverythingDistribution.VerifyExecutable(EverythingDistribution.InstalledExecutable);
            return;
        }
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
            };
            start.ArgumentList.Add(EverythingDistribution.SetupArgument);
            start.ArgumentList.Add(instance);
            using var setup = Process.Start(start) ?? throw new SearchException("无法准备全盘索引，请点击“重试搜索”。");
            await setup.WaitForExitAsync(token);
            if (setup.ExitCode != 0 || !IsRunning(instance))
                throw new SearchException("全盘索引服务未能启动。请点击“重试搜索”，并允许 Windows 管理员授权；也可暂用“指定目录”。");
        }
        catch (Win32Exception e) when (e.NativeErrorCode is 1223 or 5)
        {
            throw new SearchException("尚未获得全盘索引权限。点击“重试搜索”并允许 Windows 管理员授权；无需安装其他软件，也可先用“指定目录”。");
        }
    }

    // Called only by the short-lived elevated application invocation, before creating any UI.
    public static int Install(string instance)
    {
        if (!Regex.IsMatch(instance, "^FileAssistant-[A-F0-9]{12}$", RegexOptions.CultureInvariant)) return 2;
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 5;
        try
        {
            _ = IsRunning(instance); // Refuse to replace an existing service pointing elsewhere.
            var engine = EverythingDistribution.ReadVerifiedBytes(EverythingDistribution.BundledExecutable, EverythingDistribution.ExecutableHash);
            var license = EverythingDistribution.ReadVerifiedBytes(Path.Combine(AppContext.BaseDirectory, "SearchEngine", "License.txt"), EverythingDistribution.LicenseHash);
            var destination = EverythingDistribution.InstalledExecutable;
            var directory = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(directory);
            // Refuse redirected installation paths before writing an elevated executable.
            for (var parent = new DirectoryInfo(directory); parent is not null; parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) return 6;
            if (!File.Exists(destination)) File.WriteAllBytes(destination, engine);
            EverythingDistribution.VerifyExecutable(destination);
            File.WriteAllBytes(Path.Combine(directory, "License.txt"), license);
            var start = new ProcessStartInfo(destination) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var value in new[] { "-instance", instance, "-install-service-pipe-name", EverythingDistribution.ServicePipe(instance), "-install-service" }) start.ArgumentList.Add(value);
            using var process = Process.Start(start)!;
            if (!process.WaitForExit(30000)) return 7;
            return IsRunning(instance) ? 0 : 8;
        }
        catch { return 1; }
    }

    private static bool IsRunning(string instance)
    {
        using var serviceKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + EverythingDistribution.ServiceName(instance));
        var image = serviceKey?.GetValue("ImagePath") as string;
        if (image is null) return false;
        if (!image.StartsWith('"' + EverythingDistribution.InstalledExecutable + '"', StringComparison.OrdinalIgnoreCase))
            throw new SearchException("索引服务路径与本软件不一致。为避免影响其他程序，已停止连接，请修复本软件安装。");
        var manager = OpenSCManagerW(null, null, 1);
        if (manager == 0) return false;
        try
        {
            var service = OpenServiceW(manager, EverythingDistribution.ServiceName(instance), 4);
            if (service == 0) return false;
            try { return QueryServiceStatus(service, out var status) && status.State == 4; }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus { public uint Type, State, Controls, ExitCode, SpecificExitCode, CheckPoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern nint OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern nint OpenServiceW(nint manager, string name, uint access);
    [DllImport("advapi32.dll")] private static extern bool QueryServiceStatus(nint service, out ServiceStatus status);
    [DllImport("advapi32.dll")] private static extern bool CloseServiceHandle(nint handle);
}
