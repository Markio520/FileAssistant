using System;
using FileAssistant.App.Features.FileInput;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.Workspace;

public sealed partial class WorkspacePage : Page
{
    public WorkspaceViewModel ViewModel => App.Workspace;
    public WorkspaceViewModel CopyViewModel => App.CopyInput;

    public WorkspacePage()
    {
        InitializeComponent();
        App.Conversion.OutputsRequested += (_, task) => { App.Outputs.TaskId = task; Navigation.SelectedItem = OutputsNavigation; };
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

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (StagingPanel is not null && args.SelectedItem is NavigationViewItem item)
        {
            var searching = item.Tag is "search";
            var copying = item.Tag is "organize";
            var converting = item.Tag is "convert";
            var outputs = item.Tag is "outputs";
            var settings = item.Tag is "settings";
            if (!searching) SearchPanel.Deactivate();
            StagingPanel.Visibility = searching || copying || converting || outputs || settings ? Visibility.Collapsed : Visibility.Visible;
            CopyPanel.Visibility = copying ? Visibility.Visible : Visibility.Collapsed;
            SearchPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            ConversionPanel.Visibility = converting ? Visibility.Visible : Visibility.Collapsed;
            OutputsPanel.Visibility = outputs ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
            if (searching) SearchPanel.Activate();
        }
    }

    private void Input_SearchRequested(object? sender, EventArgs e) => Navigation.SelectedItem = SearchNavigationItem;
    private void Search_WorkspaceRequested(object? sender, EventArgs e) => Navigation.SelectedItem = WorkspaceNavigation;

    private async void Input_FilesForCopyRequested(object? sender, FileInputTransferEventArgs e)
    {
        Navigation.SelectedItem = CopyNavigation;
        await CopyViewModel.ImportFilesAsync(e.Files);
    }

    private async void Input_FilesForConversionRequested(object? sender, FileInputTransferEventArgs e)
    {
        Navigation.SelectedItem = ConversionNavigation;
        await App.Conversion.Input.ImportFilesAsync(e.Files);
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is "tasks")
        {
            if (SearchPanel.Visibility == Visibility.Visible || SettingsPanel.Visibility == Visibility.Visible) Navigation.SelectedItem = WorkspaceNavigation;
            ViewModel.IsQueueExpanded = !ViewModel.IsQueueExpanded;
        }
    }
}
