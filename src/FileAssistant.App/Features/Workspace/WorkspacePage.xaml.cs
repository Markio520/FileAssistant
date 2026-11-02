using System;
using System.ComponentModel;
using FileAssistant.App.Features.FileInput;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.Workspace;

public sealed partial class WorkspacePage : Page
{
    public WorkspaceViewModel ViewModel => App.Workspace;
    public WorkspaceViewModel CopyViewModel => App.CopyInput;
    public Tasks.ProcessingJobsViewModel Jobs => App.Jobs;

    public WorkspacePage()
    {
        InitializeComponent();
        Loaded += (_, _) => { ViewModel.PropertyChanged += QueueStateChanged; UpdateTaskQueue(); };
        Unloaded += (_, _) => { ViewModel.PropertyChanged -= QueueStateChanged; TaskQueueFlyout.Hide(); };
        ActualThemeChanged += (_, _) => UpdateQueueLayout();
        SizeChanged += (_, _) => { UpdateQueueLayout(); UpdateNavigationLayout(); };
        App.Conversion.OutputsRequested += (_, task) => { App.Outputs.TaskId = task; NavigateTo("outputs"); };
        App.Conversion.EngineManagementRequested += (_, _) => NavigateTo("settings");
        KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.F, Modifiers = Windows.System.VirtualKeyModifiers.Control });
        KeyboardAccelerators[^1].Invoked += (_, args) =>
        {
            if (SearchPanel.Visibility == Visibility.Visible) SearchPanel.FocusQuery();
            else if (CopyPanel.Visibility == Visibility.Visible) CopyPanel.FocusQuery();
            else if (ConversionPanel.Visibility == Visibility.Visible) ConversionPanel.FocusQuery();
            else if (OutputsPanel.Visibility == Visibility.Visible) OutputsPanel.FocusQuery();
            else StagingPanel.FocusQuery();
            args.Handled = true;
        };
    }

    private void Navigation_Click(object sender, RoutedEventArgs e) => NavigateTo((string)((ToggleButton)sender).Tag);

    private void NavigateTo(string page)
    {
        if (StagingPanel is not null)
        {
            foreach (var button in new[] { WorkspaceNavigation, SearchNavigationItem, CopyNavigation, ConversionNavigation, OutputsNavigation, SettingsNavigation })
                button.IsChecked = Equals(button.Tag, page);
            var searching = page is "search";
            var copying = page is "organize";
            var converting = page is "convert";
            var outputs = page is "outputs";
            var settings = page is "settings";
            if (!searching) SearchPanel.Deactivate();
            StagingPanel.Visibility = searching || copying || converting || outputs || settings ? Visibility.Collapsed : Visibility.Visible;
            CopyPanel.Visibility = copying ? Visibility.Visible : Visibility.Collapsed;
            SearchPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            ConversionPanel.Visibility = converting ? Visibility.Visible : Visibility.Collapsed;
            OutputsPanel.Visibility = outputs ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
            if (searching) SearchPanel.Activate();
            if (converting) App.Conversion.RefreshCapabilities();
            ConversionPanel.SetActive(converting);
        }
    }

    private void Input_SearchRequested(object? sender, EventArgs e) => NavigateTo("search");
    private void Search_WorkspaceRequested(object? sender, EventArgs e) => NavigateTo("workspace");

    private async void Input_FilesForCopyRequested(object? sender, FileInputTransferEventArgs e)
    {
        var report = await FileCollectionDialogs.ImportAsync(CopyViewModel, e.Bundle, this);
        if (report is not null) NavigateTo("organize");
    }

    private async void Input_FilesForConversionRequested(object? sender, FileInputTransferEventArgs e)
    {
        var report = await FileCollectionDialogs.ImportAsync(App.Conversion.Input, e.Bundle, this);
        if (report is not null) NavigateTo("convert");
    }

    private void TaskQueue_Click(object sender, RoutedEventArgs e) => ViewModel.IsQueueExpanded = !ViewModel.IsQueueExpanded;

    private void UpdateNavigationLayout()
    {
        BrandLabel.Visibility = ActualWidth < 1160 ? Visibility.Collapsed : Visibility.Visible;
        QueueLabel.Visibility = ActualWidth < 1080 ? Visibility.Collapsed : Visibility.Visible;
        OutputLabel.Visibility = ActualWidth < 980 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void QueueStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.IsQueueExpanded)) UpdateTaskQueue();
    }

    private void UpdateTaskQueue()
    {
        if (!IsLoaded) return;
        if (!ViewModel.IsQueueExpanded) { TaskQueueFlyout.Hide(); return; }
        UpdateQueueLayout();
        TaskQueueFlyout.ShowAt(TasksNavigation);
    }

    private void UpdateQueueLayout()
    {
        if (TaskQueuePanel is null) return;
        TaskQueuePanel.RequestedTheme = ActualTheme;
        TaskQueueScroll.Width = Math.Clamp(ActualWidth - 80, 240, 460);
        TaskQueueScroll.MaxHeight = Math.Max(120, ActualHeight - 80);
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(FrameworkElement.RequestedThemeProperty, ActualTheme));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 500d));
        TaskQueueFlyout.FlyoutPresenterStyle = style;
    }

    private void TaskQueueFlyout_Closed(object? sender, object e) => ViewModel.IsQueueExpanded = false;
}
