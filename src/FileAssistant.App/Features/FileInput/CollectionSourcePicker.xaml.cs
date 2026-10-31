using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.FileCollections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class CollectionSourcePicker : UserControl
{
    private readonly WorkspaceViewModel browser = new();
    private CollectionSourceSelection? selection;
    private bool syncing;
    public bool HasSelection => selection?.Count > 0;
    public FileCollectionBundle Selection => selection?.Bundle ?? new([], []);
    public event EventHandler? SelectionChanged;
    public CollectionSourcePicker()
    {
        InitializeComponent(); Files.ItemsSource = browser.VisibleFiles;
        // ListViewItem handles gestures for selection; folder activation must still receive the double tap.
        Files.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(Files_DoubleTapped), handledEventsToo: true);
    }
    public void Initialize(WorkspaceViewModel source)
    {
        selection = new(source.CollectionSnapshot);
        syncing = true;
        try { browser.InitializeCollection(source.CollectionSnapshot with { CurrentFolderId = Guid.Empty }); }
        finally { syncing = false; }
        UpdateNavigation();
    }
    private void UpdateNavigation() { PathLabel.Text = browser.CurrentFolderPath; Up.IsEnabled = browser.CanGoUp; UpdateCount(); }
    private void UpdateCount() { CountLabel.Text = $"已选 {selection?.Count ?? 0:N0} 项 · 实际 {Selection.Files.Count:N0} 个文件（含子文件夹，已去重）"; SelectionChanged?.Invoke(this, EventArgs.Empty); }
    private void Navigate(Action action)
    {
        syncing = true;
        try { action(); Files.SelectedItems.Clear(); foreach (var item in browser.VisibleFiles.Where(item => selection?.Contains(item) == true)) Files.SelectedItems.Add(item); }
        finally { syncing = false; }
        UpdateNavigation();
    }
    private void Enter(Guid folder) { selection?.BrowseInto(folder); Navigate(() => browser.EnterFolder(folder)); }
    private void Root_Click(object sender, RoutedEventArgs e) => Navigate(() => browser.EnterFolder(Guid.Empty));
    private void Up_Click(object sender, RoutedEventArgs e) => Navigate(browser.GoUp);
    private void All_Click(object sender, RoutedEventArgs e) => Files.SelectAll();
    private void Clear_Click(object sender, RoutedEventArgs e) { selection?.Clear(); Navigate(() => { }); }
    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (syncing) return; selection?.UpdateVisible(browser.VisibleFiles, Files.SelectedItems.OfType<WorkspaceFile>()); UpdateCount(); }
    private void Files_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Files.SelectedItems.Count == 1 && Files.SelectedItem is WorkspaceFile { FolderId: { } folder }) { Enter(folder); e.Handled = true; }
        else if (e.Key == VirtualKey.Back) { Navigate(browser.GoUp); e.Handled = true; }
    }
    private void Files_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ListViewItem { Content: WorkspaceFile { FolderId: { } folder } })
            {
                e.Handled = true;
                // Finish the row's selection gesture before replacing its item containers.
                DispatcherQueue.TryEnqueue(() => Enter(folder));
                return;
            }
    }
}
