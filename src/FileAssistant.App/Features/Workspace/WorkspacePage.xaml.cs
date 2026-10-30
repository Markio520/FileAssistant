using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.Workspace;

public sealed partial class WorkspacePage : Page
{
    public WorkspaceViewModel ViewModel => App.Workspace;

    public WorkspacePage()
    {
        InitializeComponent();
        KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.F, Modifiers = Windows.System.VirtualKeyModifiers.Control });
        KeyboardAccelerators[^1].Invoked += (_, args) =>
        {
            if (SearchPanel.Visibility == Visibility.Visible) SearchPanel.FocusQuery();
            else SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        };
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchText = sender.Text;
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectionCount = FileList.SelectedItems.Count;
        ViewModel.SelectedFile = e.AddedItems.OfType<WorkspaceFile>().LastOrDefault()
            ?? FileList.SelectedItems.OfType<WorkspaceFile>().FirstOrDefault();
    }

    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelection();

    private void Remove_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        RemoveSelection();
        args.Handled = true;
    }

    private void SelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        FileList.SelectAll();
        args.Handled = true;
    }

    private void RemoveSelection() => ViewModel.RemoveFromList(FileList.SelectedItems.OfType<WorkspaceFile>().ToArray());

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (Inspector is not null && args.SelectedItem is NavigationViewItem item)
        {
            var searching = item.Tag is "search";
            WorkspaceLayout.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
            SearchPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            if (searching) SearchPanel.Activate(); else SearchPanel.Deactivate();
            Inspector.ShowParameters((string)item.Tag == "organize");
        }
    }

    private void FindFiles_Click(object sender, RoutedEventArgs e) => Navigation.SelectedItem = SearchNavigationItem;
    private void Search_WorkspaceRequested(object? sender, EventArgs e) => Navigation.SelectedItem = Navigation.MenuItems[0];

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is "tasks")
        {
            ViewModel.IsQueueExpanded = !ViewModel.IsQueueExpanded;
        }
    }

    private void WindowSize_Click(object sender, RoutedEventArgs e)
    {
        if (App.Window is MainWindow window && sender is MenuFlyoutItem item)
        {
            window.SetWorkspaceSize(item.Tag is "compact");
        }
    }
}
