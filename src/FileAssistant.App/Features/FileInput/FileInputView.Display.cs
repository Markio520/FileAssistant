using System.ComponentModel;
using FileAssistant.App.Features.Workspace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class FileInputView
{
    private bool showTiles = true;
    private bool switchingView;
    private WorkspaceViewModel? displayModel;

    private void AttachDisplayModel()
    {
        if (!IsLoaded || ReferenceEquals(displayModel, ViewModel)) return;
        DetachDisplayModel();
        displayModel = ViewModel;
        if (displayModel is null) return;
        displayModel.PropertyChanged += DisplayModelChanged;
        ResetFolderNavigation();
        UpdateFolderNavigation();
    }

    private void DetachDisplayModel()
    {
        if (displayModel is not null) displayModel.PropertyChanged -= DisplayModelChanged;
        displayModel = null;
    }

    private void DisplayModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.CollectionRevision) ||
            e.PropertyName == nameof(WorkspaceViewModel.BatchSummary) && ViewModel.HasSamples) QueueFolderNavigationUpdate();
    }

    private void CollapseFolders_Click(object sender, RoutedEventArgs e) => SetFoldersOpen(false);
    private void ExpandFolders_Click(object sender, RoutedEventArgs e) => SetFoldersOpen(true);
    private void SetFoldersOpen(bool open)
    {
        FolderSidebar.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        CollapsedFolders.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        FoldersColumn.Width = new GridLength(open ? 176 : 48);
        DetailsHost.MinimumContentWidth = open ? 536 : 408;
    }
    private void CollapseInspector_Click(object sender, RoutedEventArgs e) => DetailsHost.IsPaneOpen = false;
    private void ExpandInspector_Click(object sender, RoutedEventArgs e) => DetailsHost.IsPaneOpen = true;
    private void ListMode_Click(object sender, RoutedEventArgs e) => SetFileView(false);
    private void TileMode_Click(object sender, RoutedEventArgs e) => SetFileView(true);

    private void SetFileView(bool tiles)
    {
        var template = (DataTemplate)Resources[tiles ? "FileTileTemplate" : "FileTableTemplate"];
        ListModeButton.IsChecked = !tiles;
        TileModeButton.IsChecked = tiles;
        if (ReferenceEquals(FileList.ItemTemplate, template)) return;
        var selection = FileList.SelectedItems.OfType<WorkspaceFile>().ToArray();
        switchingView = true;
        try
        {
            showTiles = tiles;
            FilesHeader.Visibility = tiles ? Visibility.Collapsed : Visibility.Visible;
            FilesHeaderRow.Height = tiles ? new GridLength(0) : GridLength.Auto;
            FilesViewport.HorizontalScrollMode = tiles ? ScrollMode.Disabled : ScrollMode.Enabled;
            FilesViewport.HorizontalScrollBarVisibility = tiles ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            TableContent.MinWidth = tiles ? 0 : ColumnWidths.MinimumTableWidth;
            FileList.ItemTemplate = template;
            FileList.ItemsPanel = (ItemsPanelTemplate)Resources[tiles ? "FileTilePanel" : "FileTablePanel"];
            FileList.ItemContainerStyle = (Style)Application.Current.Resources[tiles ? "WorkspaceTileItemStyle" : "WorkspaceListItemStyle"];
            foreach (var item in selection)
                if (FileList.Items.Contains(item) && !FileList.SelectedItems.Contains(item)) FileList.SelectedItems.Add(item);
        }
        finally { switchingView = false; }
        DispatcherQueue.TryEnqueue(UpdateTileWidth);
    }

    private void FileList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTileWidth();
    private void UpdateTileWidth()
    {
        if (!showTiles || FileList.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        var width = Math.Max(1, FileList.ActualWidth - 18);
        var columns = Math.Max(1, (int)(width / 184));
        panel.ItemWidth = Math.Floor(width / columns);
    }

    private void FilesToolbar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var compact = width < 760;
        var narrow = width < 520;
        FolderIdentity.MaxWidth = compact ? 220 : Math.Clamp(width - 580, 160, 240);
        Grid.SetColumnSpan(FolderIdentity, narrow ? 3 : 1);
        Grid.SetRow(FilterSearch, compact ? 1 : 0);
        Grid.SetColumn(FilterSearch, compact ? 0 : 1);
        Grid.SetColumnSpan(FilterSearch, compact ? 3 : 1);
        FilterSearch.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 12, 0);
        Grid.SetRow(ViewActions, narrow ? 2 : 0);
        Grid.SetColumn(ViewActions, narrow ? 0 : compact ? 1 : 2);
        Grid.SetColumnSpan(ViewActions, narrow ? 3 : compact ? 2 : 1);
        ViewActions.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        ActionsDivider.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }
}
