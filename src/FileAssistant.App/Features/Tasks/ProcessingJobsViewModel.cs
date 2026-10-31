using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileAssistant.App.Features.Tasks;

public partial class ProcessingJobsViewModel : ObservableObject
{
    private int sequence;
    public ObservableCollection<ProcessingJob> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !HasItems;
    public string Summary => $"{Items.Count(job => job.IsRunning)} 项正在处理 · 共 {Items.Count} 项任务";
    public ProcessingJob Start(Action cancel)
    {
        var job = new ProcessingJob(++sequence, $"格式转换 {sequence}", cancel);
        job.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Summary));
        Items.Insert(0, job);
        OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(Summary));
        return job;
    }
}
