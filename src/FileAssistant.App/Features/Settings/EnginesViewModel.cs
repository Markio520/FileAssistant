using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Engines;

namespace FileAssistant.App.Features.Settings;

public partial class EnginesViewModel(IFileProcessingService processing, IEnginePackageInstaller installer, EnginePackage videoPackage) : ObservableObject
{
    private CancellationTokenSource? cancellation;
    public ObservableCollection<ProcessingEngineInfo> Engines { get; } = [];
    public string DownloadDescription => $"{videoPackage.Name} {videoPackage.Version} · Windows x64 · {videoPackage.DownloadBytes / 1_000_000d:0.0} MB · {videoPackage.License}";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand))] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string Feedback { get; set; } = string.Empty;
    private bool CanDownload => !IsBusy;
    [RelayCommand] private async Task RefreshAsync()
    {
        var engines = await Task.Run(() => processing.Engines);
        Engines.Clear(); foreach (var engine in engines) Engines.Add(engine);
    }
    [RelayCommand(CanExecute = nameof(CanDownload))] private async Task DownloadVideoAsync()
    {
        IsBusy = true; Progress = 0; Feedback = "正在下载视频引擎…";
        using var current = new CancellationTokenSource(); cancellation = current;
        try
        {
            var progress = new Progress<int>(value => Progress = value);
            await Task.Run(() => installer.InstallAsync(videoPackage, progress, current.Token));
            Progress = 100; Feedback = "视频引擎已就绪。";
            await RefreshAsync();
        }
        catch (OperationCanceledException) { Feedback = "已取消下载，现有引擎保留。"; }
        catch (FileProcessingException error) { Feedback = error.Message; }
        finally { cancellation = null; IsBusy = false; }
    }
    [RelayCommand] private void Cancel() => cancellation?.Cancel();
}
