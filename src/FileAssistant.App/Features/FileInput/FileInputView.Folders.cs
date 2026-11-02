using FileAssistant.App.Features.Workspace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class FileInputView
{
    private readonly Dictionary<Guid, TreeViewNode> folderNodes = [];
    private bool updatingFolderTree;
    private bool folderUpdateQueued;
    private Guid? shownFolderId;

    private void ResetFolderNavigation()
    {
        updatingFolderTree = true;
        try { FolderTree.RootNodes.Clear(); folderNodes.Clear(); shownFolderId = null; }
        finally { updatingFolderTree = false; }
    }

    private void QueueFolderNavigationUpdate()
    {
        if (folderUpdateQueued) return;
        folderUpdateQueued = true;
        // Navigation changes the input list during TreeView's selection event.
        // Reconcile after that event so native keyboard focus and selection finish first.
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            folderUpdateQueued = false;
            if (IsLoaded && displayModel is not null) UpdateFolderNavigation();
        })) folderUpdateQueued = false;
    }

    private void UpdateFolderNavigation()
    {
        var folders = ViewModel.FolderNavigation;
        var ids = folders.Select(folder => folder.Id).ToHashSet();
        updatingFolderTree = true;
        try
        {
            foreach (var id in folderNodes.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                var node = folderNodes[id];
                node.Parent?.Children.Remove(node);
                folderNodes.Remove(id);
            }
            foreach (var folder in folders)
            {
                if (!folderNodes.TryGetValue(folder.Id, out var node))
                    folderNodes[folder.Id] = node = new TreeViewNode { IsExpanded = true };
                if (!Equals(node.Content, folder)) node.Content = folder;
            }
            var root = folderNodes[Guid.Empty];
            if (FolderTree.RootNodes.Count == 0) FolderTree.RootNodes.Add(root);
            // Detach old relationships first; undo can reverse a parent/child move.
            foreach (var folder in folders.Where(folder => folder.Id != Guid.Empty))
            {
                var node = folderNodes[folder.Id];
                if (!ReferenceEquals(node.Parent, folderNodes[folder.ParentId])) node.Parent?.Children.Remove(node);
            }
            foreach (var folder in folders.Where(folder => folder.Id != Guid.Empty))
            {
                var node = folderNodes[folder.Id];
                var parent = folderNodes[folder.ParentId];
                if (ReferenceEquals(node.Parent, parent)) continue;
                parent.Children.Add(node);
            }
            var current = folderNodes[ViewModel.CurrentFolderId];
            if (shownFolderId != ViewModel.CurrentFolderId)
                for (var parent = current.Parent; parent is not null; parent = parent.Parent) parent.IsExpanded = true;
            if (!ReferenceEquals(FolderTree.SelectedNode, current)) FolderTree.SelectedNode = current;
            shownFolderId = ViewModel.CurrentFolderId;
            CurrentFolderTitle.Text = ViewModel.CurrentFolderId == Guid.Empty ? "全部文件" : ViewModel.CurrentFolderName;
        }
        finally { updatingFolderTree = false; }
    }

    private void FolderTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e)
    {
        if (updatingFolderTree || sender.SelectedNode?.Content is not FolderNavigationItem folder) return;
        ViewModel.EnterFolder(folder.Id);
        QueueFolderNavigationUpdate();
    }

    private TreeViewItem? FolderContainer(DependencyObject? source)
    {
        for (; source is not null && source != FolderTreeArea; source = VisualTreeHelper.GetParent(source))
            if (source is TreeViewItem item) return item;
        return null;
    }

    private Guid DropFolderId(TreeViewItem? container) =>
        (container is not null ? FolderTree.NodeFromContainer(container)?.Content as FolderNavigationItem : null)?.Id ?? Guid.Empty;

    private TreeViewItem? DropFolderContainer(DragEventArgs e)
    {
        // Drag events can be retargeted to the surrounding grid by the native TreeView.
        // Hit-test row bounds so dropping on a label, count, chevron or row padding has one destination.
        var point = e.GetPosition(FolderTreeArea);
        foreach (var node in folderNodes.Values)
            if (FolderTree.ContainerFromNode(node) is TreeViewItem { IsLoaded: true } item &&
                item.TransformToVisual(FolderTreeArea).TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight)).Contains(point)) return item;
        return null;
    }

    private void FolderTree_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs e)
    {
        var ids = e.Items.OfType<TreeViewNode>().Select(node => node.Content).OfType<FolderNavigationItem>().Select(folder => folder.Id).ToArray();
        e.Cancel = ids.Contains(Guid.Empty) || !StartInputDrag(ViewModel.GetFolderItems(ids), e.Data);
    }

    private void FolderTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs e)
    { currentDrag = null; ClearDropHighlight(); }

    private void FolderTree_DragOver(object sender, DragEventArgs e)
    {
        var container = DropFolderContainer(e);
        if (!CollectionDragOver(e, DropFolderId(container))) { ClearDropHighlight(); return; }
        dropHighlight.Show(container ?? (DependencyObject)FolderTreeArea);
    }

    private async void FolderTree_Drop(object sender, DragEventArgs e)
    {
        var destination = DropFolderId(DropFolderContainer(e));
        await CollectionDropAsync(e, destination);
        if (folderNodes.TryGetValue(destination, out var parent)) parent.IsExpanded = true;
    }

    private void FolderTree_DragLeave(object sender, DragEventArgs e) => ClearDropHighlight();

    private void FolderTree_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        e.Handled = true;
        var container = FolderContainer(e.OriginalSource as DependencyObject);
        var hasPosition = e.TryGetPosition(FolderTreeArea, out var position);
        var node = container is not null ? FolderTree.NodeFromContainer(container) : hasPosition ? null : FolderTree.SelectedNode;
        var parentId = (node?.Content as FolderNavigationItem)?.Id ?? Guid.Empty;
        var menu = new MenuFlyout { MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["ActionDropDownPresenterStyle"] };
        var create = new MenuFlyoutItem
        {
            Text = parentId == Guid.Empty ? "新建文件夹…" : "新建子文件夹…",
            Icon = new FontIcon { Glyph = "\uE8F4" },
            IsEnabled = ViewModel.CanReceiveFiles,
            Style = (Style)Application.Current.Resources["ActionDropDownItemStyle"]
        };
        create.Click += async (_, _) => await FolderDialogAsync(async () =>
        {
            await FileCollectionDialogs.RenameOrCreateAsync(ViewModel, this, parent: parentId);
            if (folderNodes.TryGetValue(parentId, out var parent)) parent.IsExpanded = true;
        });
        menu.Items.Add(create);
        if (hasPosition) menu.ShowAt(FolderTreeArea, new FlyoutShowOptions { Position = position });
        else menu.ShowAt(container ?? (FrameworkElement)FolderTree);
    }
}
