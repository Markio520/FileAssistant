using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileAssistant.Core.Engines;

namespace FileAssistant.App.Features.Tasks;

public partial class ProcessingJob(int id, string name, Action cancel) : ObservableObject
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] public partial string Status { get; set; } = "正在处理";
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CancelCommand))] public partial bool IsRunning { get; set; } = true;
    public ObservableCollection<string> Details { get; } = [];
    [RelayCommand(CanExecute = nameof(IsRunning))] private void Cancel() { if (IsRunning) { Status = "正在取消…"; cancel(); } }
    public void Update(ProcessingProgress value)
    { if (IsRunning) { Progress = value.Total == 0 ? 0 : value.Completed * 100d / value.Total; Status = value.Message; } }
    public void Complete(ConversionReport report)
    {
        IsRunning = false; Progress = report.Cancelled ? Progress : 100; Status = report.Summary;
        foreach (var item in report.Items) Details.Add($"{item.Item.SourceLabel}：{item.Message}");
    }
    public void Fail(string message) { IsRunning = false; Status = message; Details.Add(message); }
}
