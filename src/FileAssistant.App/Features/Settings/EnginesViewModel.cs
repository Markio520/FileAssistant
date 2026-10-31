using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Engines;

namespace FileAssistant.App.Features.Settings;

public partial class EnginesViewModel(IFileProcessingService processing, IEnginePackageInstaller installer, EnginePackage videoPackage, EnginePackage officePackage) : ObservableObject
{
    private CancellationTokenSource? cancellation;
    public ObservableCollection<ProcessingEngineInfo> Engines { get; } = [];
    public string DownloadDescription => $"{videoPackage.Name} {videoPackage.Version} · Windows x64 · {videoPackage.DownloadBytes / 1_000_000d:0.0} MB · {videoPackage.License}";
    public string OfficeDownloadDescription => $"{officePackage.Name} {officePackage.Version} · Windows x64 · 下载 {officePackage.DownloadBytes / 1_000_000d:0.0} MB，安装约 1.2 GB · {officePackage.License}";
    public event EventHandler? AvailabilityChanged;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand), nameof(DownloadOfficeCommand))] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    private bool CanDownload => !IsBusy;
    [RelayCommand] private async Task RefreshAsync()
    {
        var engines = await Task.Run(() => processing.Engines);
        Engines.Clear(); foreach (var engine in engines) Engines.Add(engine);
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand(CanExecute = nameof(CanDownload))] private Task DownloadVideoAsync() => DownloadAsync(videoPackage);
    [RelayCommand(CanExecute = nameof(CanDownload))] private Task DownloadOfficeAsync() => DownloadAsync(officePackage);
    private async Task DownloadAsync(EnginePackage package)
    {
        if (IsBusy) return;
        IsBusy = true; Progress = 0; Feedback = $"正在下载 {package.Name}…";
        using var current = new CancellationTokenSource(); cancellation = current;
        try
        {
            var progress = new Progress<int>(value => { Progress = value; if (value >= 90 && value < 100) Feedback = "正在解包和检查引擎；取消时将等待解包结束后清理。"; });
            await Task.Run(() => installer.InstallAsync(package, progress, current.Token));
            Progress = 100; Feedback = $"{package.Name} 已就绪。";
            await RefreshAsync();
        }
        catch (OperationCanceledException) { Feedback = "已取消下载，现有引擎保留。"; }
        catch (FileProcessingException error) { Feedback = error.Message; }
        finally { cancellation = null; IsBusy = false; }
    }
    [RelayCommand] private void Cancel() => cancellation?.Cancel();
}
