using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileAssistant.App.Features.Tasks;

public partial class ProcessingJobsViewModel : ObservableObject
{
    private int sequence;
    public ObservableCollection<ProcessingJob> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !HasItems;
    public int RunningCount => Items.Count(job => job.IsRunning);
    public bool HasRunningJobs => RunningCount > 0;
    public string Summary => $"{RunningCount} 项正在处理 · 共 {Items.Count} 项任务";
    public ProcessingJob Start(Action cancel)
    {
        var job = new ProcessingJob(++sequence, $"格式转换 {sequence}", cancel);
        job.PropertyChanged += (_, _) => NotifyProgress();
        Items.Insert(0, job);
        OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(IsEmpty)); NotifyProgress();
        return job;
    }
    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(RunningCount)); OnPropertyChanged(nameof(HasRunningJobs));
    }
}
