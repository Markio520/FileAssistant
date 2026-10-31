using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Workspace;

public sealed partial class WorkspaceTaskPanel : UserControl
{
    public Tasks.ProcessingJobsViewModel Jobs => App.Jobs;
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WorkspaceViewModel), typeof(WorkspaceTaskPanel), new PropertyMetadata(null));

    public WorkspaceViewModel ViewModel
    {
        get => (WorkspaceViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public WorkspaceTaskPanel() => InitializeComponent();
}
