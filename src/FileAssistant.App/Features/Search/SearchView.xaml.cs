using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Windows.Storage.Pickers;
using Windows.System;

namespace FileAssistant.App.Features.Search;

public sealed partial class SearchView : UserControl
{
    private bool resizingColumn;
    public SearchViewModel ViewModel => App.Search;
    public event EventHandler? WorkspaceRequested;

    public SearchView() { InitializeComponent(); }
    public void Activate() { ViewModel.Activate(); QueryBox.Focus(FocusState.Programmatic); }
    public void Deactivate() => ViewModel.Deactivate();
    public void FocusQuery() => QueryBox.Focus(FocusState.Programmatic);

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
    private void Add_Click(object sender, RoutedEventArgs e) => ViewModel.AddSelection(ResultsList.SelectedItems.OfType<SearchResultItem>().ToArray());
    private void Workspace_Click(object sender, RoutedEventArgs e) => WorkspaceRequested?.Invoke(this, EventArgs.Empty);
    private void FolderScope_Click(object sender, RoutedEventArgs e) { ViewModel.ScopeIndex = 1; FolderBox.Focus(FocusState.Programmatic); }
    private void ResultsHeader_SizeChanged(object sender, SizeChangedEventArgs e) => ResultsHeaderRow.Height = new GridLength(e.NewSize.Height);
    private void ResultsViewport_SizeChanged(object sender, SizeChangedEventArgs e) => ViewModel.ColumnWidths.FitViewport(e.NewSize.Width);
    private void ColumnResize_Started(object sender, DragStartedEventArgs e) => resizingColumn = true;
    private void ColumnResize_Delta(object sender, DragDeltaEventArgs e) => ViewModel.ColumnWidths.Resize((string)((Thumb)sender).Tag, e.HorizontalChange);
    private void ColumnResize_Completed(object sender, DragCompletedEventArgs e) { resizingColumn = false; ProtectedCursor = null; }
    private void ColumnResize_Entered(object sender, PointerRoutedEventArgs e) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private void ColumnResize_Exited(object sender, PointerRoutedEventArgs e) { if (!resizingColumn) ProtectedCursor = null; }
    private void ColumnResize_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
        ViewModel.ColumnWidths.Resize((string)((Thumb)sender).Tag, e.Key == VirtualKey.Left ? -8 : 8);
        e.Handled = true;
    }

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
        menu.Items.Add(Item("加入操作批次", "\uE710", () => ViewModel.AddSelection(selection), ViewModel.CanAdd));
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
    private void Details_Changed(object sender, RoutedEventArgs e)
    {
        if (DetailsPanel is not null) DetailsPanel.Visibility = DetailsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { ResultsList.SelectAll(); args.Handled = true; }
    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { ViewModel.RefreshCommand.Execute(null); args.Handled = true; }
    private void Query_CompositionStarted(TextBox sender, TextCompositionStartedEventArgs args) => ViewModel.SetInputComposition(true);
    private void Query_CompositionEnded(TextBox sender, TextCompositionEndedEventArgs args) => ViewModel.SetInputComposition(false);
}
