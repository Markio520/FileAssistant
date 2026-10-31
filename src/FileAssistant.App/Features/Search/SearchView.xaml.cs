using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using FileAssistant.Core.Features.Search;

namespace FileAssistant.App.Features.Search;

public sealed partial class SearchView : UserControl
{
    private bool active;
    private object? savedResults;
    private double savedVerticalOffset;
    private double savedHorizontalOffset;
    public SearchViewModel ViewModel => App.Search;
    public event EventHandler? WorkspaceRequested;

    public SearchView() { InitializeComponent(); }
    public void Activate()
    {
        active = true;
        ViewModel.Activate();
        QueryBox.Focus(FocusState.Programmatic);
        // Restore after the collapsed page has been measured, only for the same result collection.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (!active || !ReferenceEquals(savedResults, ViewModel.Results)) return;
            ResultsList.UpdateLayout();
            FindScrollViewer(ResultsList)?.ChangeView(null, savedVerticalOffset, null, true);
            ResultsViewport.ChangeView(savedHorizontalOffset, null, null, true);
        });
    }
    public void Deactivate()
    {
        if (!active) return;
        active = false;
        savedResults = ViewModel.Results;
        savedVerticalOffset = FindScrollViewer(ResultsList)?.VerticalOffset ?? 0;
        savedHorizontalOffset = ResultsViewport.HorizontalOffset;
        ViewModel.Deactivate();
    }
    public void FocusQuery() => QueryBox.Focus(FocusState.Programmatic);

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer) return viewer;
            if (FindScrollViewer(child) is { } nested) return nested;
        }
        return null;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null) ViewModel.Folder = folder.Path;
        }
        catch (Exception) { ViewModel.Feedback = "无法打开目录选择器，请在路径框中输入文件夹路径。"; }
    }
    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectionCount = ResultsList.SelectedItems.Count;
        ViewModel.SelectedItem = e.AddedItems.OfType<SearchResultItem>().LastOrDefault() ?? ResultsList.SelectedItems.OfType<SearchResultItem>().FirstOrDefault();
    }
    private async void Add_Click(object sender, RoutedEventArgs e) => await ViewModel.AddSelectionAsync(ResultsList.SelectedItems.OfType<SearchResultItem>().ToArray());
    private void Workspace_Click(object sender, RoutedEventArgs e) => WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    private void FolderScope_Click(object sender, RoutedEventArgs e) { ViewModel.ScopeIndex = 1; FolderBox.Focus(FocusState.Programmatic); }
    private void ResultsHeader_SizeChanged(object sender, SizeChangedEventArgs e) => ResultsHeaderRow.Height = new GridLength(e.NewSize.Height);
    private void ResultsViewport_SizeChanged(object sender, SizeChangedEventArgs e) => ViewModel.ColumnWidths.FitViewport(e.NewSize.Width);
    private void Column_ResizeRequested(object? sender, double delta)
        => ViewModel.ColumnWidths.Resize((string)((FrameworkElement)sender!).Tag, delta);

    private ListViewItem? ResultContainer(object? source)
    {
        var element = source as DependencyObject;
        while (element is not null && element != ResultsList)
        {
            if (element is ListViewItem container) return container;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void Results_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (!ViewModel.CanUseResults) return;
        var pointer = e.TryGetPosition(ResultsList, out var position);
        var container = ResultContainer(pointer ? e.OriginalSource : FocusManager.GetFocusedElement(XamlRoot));
        var target = container?.Content as SearchResultItem ?? (pointer ? null : ViewModel.SelectedItem);
        if (target is null || !ViewModel.Results.Contains(target)) return;
        if (!ResultsList.SelectedItems.Contains(target))
        {
            ResultsList.SelectedItems.Clear();
            ResultsList.SelectedItems.Add(target);
        }
        var selection = ResultsList.SelectedItems.OfType<SearchResultItem>().ToArray();
        var menu = new MenuFlyout();
        MenuFlyoutItem Item(string text, string glyph, Action action, bool enabled = true)
        {
            var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph }, IsEnabled = enabled };
            item.Click += (_, _) => action();
            return item;
        }
        menu.Items.Add(Item(target.File.IsFolder ? "打开文件夹" : "打开文件", "\uE8E5", () => ViewModel.OpenFile(target), selection.Length == 1));
        menu.Items.Add(Item("打开所在文件夹", "\uED25", () => ViewModel.OpenLocation(target), selection.Length == 1));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("复制完整路径", "\uE8C8", () => ViewModel.CopySelection(selection, false)));
        menu.Items.Add(Item("复制文件名", "\uE8A5", () => ViewModel.CopySelection(selection, true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("加入暂存区", "\uE710", async () => await ViewModel.AddSelectionAsync(selection), ViewModel.CanAdd));
        if (pointer) menu.ShowAt(ResultsList, new FlyoutShowOptions { Position = position });
        else menu.ShowAt(container ?? (FrameworkElement)ResultsList);
        e.Handled = true;
    }

    private void Results_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultContainer(e.OriginalSource)?.Content is not SearchResultItem item) return;
        ViewModel.OpenFile(item);
        e.Handled = true;
    }
    private void Open_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (ResultsList.SelectedItems.Count == 1 && ViewModel.SelectedItem is { } item) ViewModel.OpenFile(item);
        e.Handled = true;
    }
    private void SortHeader_Click(object sender, RoutedEventArgs e) => ViewModel.SortBy((SearchSort)int.Parse((string)((Button)sender).Tag));
    private void ClearDate_Click(object sender, RoutedEventArgs e)
    {
        if ((string)((Button)sender).Tag == "From") ViewModel.CustomDateFrom = null;
        else ViewModel.CustomDateThrough = null;
    }
    private void SelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { ResultsList.SelectAll(); args.Handled = true; }
    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (ViewModel.RefreshCommand.CanExecute(null)) ViewModel.RefreshCommand.Execute(null); args.Handled = true; }
    private void Query_CompositionStarted(TextBox sender, TextCompositionStartedEventArgs args) => ViewModel.SetInputComposition(true);
    private void Query_CompositionEnded(TextBox sender, TextCompositionEndedEventArgs args) => ViewModel.SetInputComposition(false);
}
