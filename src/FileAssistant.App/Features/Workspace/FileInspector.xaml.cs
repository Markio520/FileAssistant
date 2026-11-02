using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Workspace;

public sealed partial class FileInspector : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WorkspaceViewModel), typeof(FileInspector), new PropertyMetadata(null));

    public WorkspaceViewModel ViewModel
    {
        get => (WorkspaceViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public FileInspector() => InitializeComponent();

    public void ShowParameters(bool show, UIElement? operationContent = null, bool hideSingleTabHeader = false)
    {
        CustomParameters.Content = operationContent;
        CustomParameters.Visibility = operationContent is null ? Visibility.Collapsed : Visibility.Visible;
        CopyParameters.Visibility = operationContent is null ? Visibility.Visible : Visibility.Collapsed;
        // Pivot still renders a collapsed item's header; remove the unavailable tab instead.
        if (!show) InspectorTabs.Items.Remove(ParametersTab);
        else if (!InspectorTabs.Items.Contains(ParametersTab)) InspectorTabs.Items.Add(ParametersTab);
        InspectorTabs.SelectedIndex = show ? 1 : 0;
        var single = !show && hideSingleTabHeader;
        if (single)
        {
            DetailsTab.Content = null;
            SingleFilePresenter.Content = DetailsContent;
        }
        else
        {
            SingleFilePresenter.Content = null;
            DetailsTab.Content = DetailsContent;
        }
        InspectorTabs.Visibility = single ? Visibility.Collapsed : Visibility.Visible;
        SingleFilePresenter.Visibility = single ? Visibility.Visible : Visibility.Collapsed;
    }
}
