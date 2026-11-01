using FileAssistant.App.Features.Workspace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class FileInputView
{
    private const string DragKey = "FileAssistant.SoftwareFolderDrag";
    private sealed record InputDrag(string Token, WorkspaceViewModel Source, IReadOnlyList<WorkspaceFile> Items);
    private static InputDrag? currentDrag;
    private Panel? dropHighlight;
    private Brush? previousDropBackground;
    private async Task FolderDialogAsync(Func<Task> action)
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        choosing = true;
        try { await action(); }
        catch (ArgumentException error) { ViewModel.InputFeedback = error.Message; }
        catch (Exception) { ViewModel.InputFeedback = "无法完成软件文件夹操作，请关闭其他弹窗后重试。"; }
        finally { choosing = false; }
    }
    private async void NewFolder_Click(object sender, RoutedEventArgs e) => await FolderDialogAsync(() => FileCollectionDialogs.RenameOrCreateAsync(ViewModel, this));
    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanRenameFolder) await FolderDialogAsync(() => FileCollectionDialogs.RenameOrCreateAsync(ViewModel, this, ViewModel.SelectedFolderIds.Single()));
    }
    private async void MoveFolder_Click(object sender, RoutedEventArgs e)
    { var items = FileList.SelectedItems.OfType<WorkspaceFile>().ToArray(); if (items.Length > 0) await FolderDialogAsync(() => FileCollectionDialogs.MoveAsync(ViewModel, items, this)); }
    private async void AlsoAdd_Click(object sender, RoutedEventArgs e)
    { var items = FileList.SelectedItems.OfType<WorkspaceFile>().ToArray(); if (items.Length > 0) await FolderDialogAsync(() => FileCollectionDialogs.MoveAsync(ViewModel, items, this, true)); }
    private async void DissolveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanRenameFolder) return;
        var id = ViewModel.SelectedFolderIds.Single();
        await FolderDialogAsync(async () =>
        {
            var dialog = FileCollectionDialogs.Dialog(this, "解散文件夹？", new TextBlock
                { Text = "内容将移到上一级，原文件位置不变。可撤销本次操作。", TextWrapping = TextWrapping.Wrap }, "解散");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.DissolveFolder(id);
        });
    }
    private async void Clear_Click(object sender, RoutedEventArgs e) => await FolderDialogAsync(async () =>
    {
        var dialog = FileCollectionDialogs.Dialog(this, "清空当前文件夹？", new TextBlock
            { Text = $"将移出“{ViewModel.CurrentFolderName}”及子文件夹中的 {ViewModel.ScopeEntries.Count:N0} 项，保留文件夹结构和原文件。可撤销本次操作。", TextWrapping = TextWrapping.Wrap }, "清空");
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.ClearCommand.Execute(null);
    });
    private async void RetrySave_Click(object sender, RoutedEventArgs e) => await ViewModel.FlushCollectionAsync();
    private async void RefreshReferences_Click(object sender, RoutedEventArgs e)
    {
        try { await ViewModel.RefreshReferencesAsync(); }
        catch (Exception) { ViewModel.InputFeedback = "检查失败，现有引用保留。请稍后重试。"; }
    }
    private void FolderUp_Click(object sender, RoutedEventArgs e) => ViewModel.GoUp();
    private void CollectionBreadcrumb_Click(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    { if (args.Item is FolderBreadcrumb folder) ViewModel.EnterFolder(folder.Id); }
    private static WorkspaceFile? RowAt(object? original)
    {
        for (var node = original as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ListViewItem { Content: WorkspaceFile row }) return row;
        return null;
    }
    private void Files_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (choosing || !ViewModel.CanReceiveFiles || RowAt(e.OriginalSource)?.FolderId is not { } id) return;
        e.Handled = true;
        var target = ViewModel;
        // Finish the row selection gesture before replacing its item containers.
        DispatcherQueue.TryEnqueue(() => { if (!choosing && ReferenceEquals(ViewModel, target)) target.EnterFolder(id); });
    }
    private async void Files_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SelectionCount == 1 && ViewModel.SelectedFolderIds.Count == 1) { ViewModel.EnterFolder(ViewModel.SelectedFolderIds[0]); e.Handled = true; }
        else if (e.Key == VirtualKey.Back) { ViewModel.GoUp(); e.Handled = true; }
        else if (e.Key == VirtualKey.F2 && ViewModel.CanRenameFolder)
        { e.Handled = true; await FolderDialogAsync(() => FileCollectionDialogs.RenameOrCreateAsync(ViewModel, this, ViewModel.SelectedFolderIds.Single())); }
    }
    private void Files_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var row = RowAt(e.OriginalSource);
        if (row is null) FileList.SelectedItems.Clear();
        else if (!FileList.SelectedItems.Contains(row)) { FileList.SelectedItems.Clear(); FileList.SelectedItems.Add(row); }
    }
    private void CollectionMenu_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        MenuFlyoutItem Item(string text, RoutedEventHandler click)
        {
            var item = new MenuFlyoutItem { Text = text, Style = (Style)Application.Current.Resources["ActionDropDownItemStyle"], IsEnabled = ViewModel.CanReceiveFiles };
            item.Click += click; return item;
        }
        MenuFlyoutSubItem Submenu(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["ActionDropDownSubItemStyle"] };
        if (!ViewModel.HasSelection)
        {
            menu.Items.Add(Item("新建文件夹…", NewFolder_Click));
            menu.Items.Add(Item("添加文件…", PickFiles_Click));
            return;
        }
        if (ViewModel.CanRenameFolder)
        {
            menu.Items.Add(Item("打开文件夹", (_, _) => ViewModel.EnterFolder(ViewModel.SelectedFolderIds.Single())));
            menu.Items.Add(Item("重命名…", RenameFolder_Click));
        }
        if (!ViewModel.HasSamples)
        {
            menu.Items.Add(Item("移动到…", MoveFolder_Click));
            menu.Items.Add(Item("添加到其他文件夹…", AlsoAdd_Click));
            var send = Submenu("用于功能");
            if (!ViewModel.IsStaging) send.Items.Add(Item("加入暂存区…", AddToStaging_Click));
            if (ViewModel.Purpose != WorkspaceInputPurpose.Copy) send.Items.Add(Item("复制与整理…", SendSelected_Click));
            if (ViewModel.Purpose != WorkspaceInputPurpose.Conversion) send.Items.Add(Item("格式转换…", ConvertSelected_Click));
            menu.Items.Add(send);
            var more = Submenu("更多");
            if (ViewModel.CanRenameFolder) more.Items.Add(Item("解散文件夹，内容移到上一级…", DissolveFolder_Click));
            more.Items.Add(Item("重新检查文件状态", RefreshReferences_Click));
            menu.Items.Add(more);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("从列表移除", Remove_Click));
    }
    private void Files_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var items = e.Items.OfType<WorkspaceFile>().ToArray();
        if (!ViewModel.CanReceiveFiles || choosing || ViewModel.HasSamples || items.Length == 0) { e.Cancel = true; return; }
        currentDrag = new(Guid.NewGuid().ToString("N"), ViewModel, items);
        e.Data.SetData(DragKey, currentDrag.Token);
        e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;
    }
    private void Files_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) { currentDrag = null; ClearDropHighlight(); }
    private static InputDrag? InternalDrag(DataPackageView data) => currentDrag is { } drag && data.Contains(DragKey) ? drag : null;
    private bool CanDrop(DataPackageView data, Guid destination)
    {
        var drag = InternalDrag(data);
        return ViewModel.CanReceiveFiles && !choosing && ViewModel.HasFolder(destination) && (drag is not null || FileInputSources.CanRead(data)) &&
            (drag is null || drag.Source != ViewModel || ViewModel.CanMoveTo(drag.Items.Where(item => item.FolderId is not null).Select(item => item.FolderId!.Value).ToArray(), destination));
    }
    private bool CollectionDragOver(DragEventArgs e, Guid destination)
    {
        var drag = InternalDrag(e.DataView); var valid = CanDrop(e.DataView, destination);
        e.AcceptedOperation = !valid ? DataPackageOperation.None : drag?.Source == ViewModel ? DataPackageOperation.Move : DataPackageOperation.Copy;
        var name = ViewModel.HasFolder(destination) ? destination == Guid.Empty ? ViewModel.Title : ViewModel.FolderName(destination) : "此位置";
        e.DragUIOverride.Caption = !valid ? "不能加入此位置" : (drag?.Source == ViewModel ? "移入“" : "加入“") + name + "” · 原文件位置不变";
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
        return valid;
    }
    private void ClearDropHighlight()
    {
        if (dropHighlight is not null) dropHighlight.Background = previousDropBackground;
        dropHighlight = null; previousDropBackground = null;
    }
    private void Row_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not Panel { Tag: WorkspaceFile { FolderId: { } id } } panel) return;
        if (!CollectionDragOver(e, id)) { ClearDropHighlight(); return; }
        if (ReferenceEquals(dropHighlight, panel)) return;
        ClearDropHighlight(); dropHighlight = panel; previousDropBackground = panel.Background;
        panel.Background = (Brush)Application.Current.Resources["WorkspaceAccentSubtleBrush"];
    }
    private void Row_DragLeave(object sender, DragEventArgs e) { if (ReferenceEquals(sender, dropHighlight)) ClearDropHighlight(); }
    private async void Row_Drop(object sender, DragEventArgs e)
    { if ((sender as FrameworkElement)?.Tag is WorkspaceFile { FolderId: { } id }) await CollectionDropAsync(e, id); }
    private async Task CollectionDropAsync(DragEventArgs e, Guid destination)
    {
        ClearDropHighlight();
        if (!CanDrop(e.DataView, destination)) { e.Handled = true; e.AcceptedOperation = DataPackageOperation.None; return; }
        var deferral = e.GetDeferral(); e.Handled = true;
        try
        {
            if (InternalDrag(e.DataView) is { } drag)
            {
                if (await e.DataView.GetDataAsync(DragKey) is not string token || token != drag.Token)
                { e.AcceptedOperation = DataPackageOperation.None; return; }
                if (drag.Source == ViewModel) { e.AcceptedOperation = DataPackageOperation.Move; ViewModel.MoveItemsTo(drag.Items, destination); }
                else { e.AcceptedOperation = DataPackageOperation.Copy; await ViewModel.ImportBundleAsync(drag.Source.ExportItems(drag.Items), destination, true); }
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                var paths = await FileInputSources.ReadAsync(e.DataView);
                if (paths.Count == 0) ViewModel.InputFeedback = "未发现可添加的文件。";
                else await ViewModel.ImportPathsAsync(paths, destination);
            }
        }
        catch (ArgumentException error) { e.AcceptedOperation = DataPackageOperation.None; ViewModel.InputFeedback = error.Message; }
        catch (Exception) { e.AcceptedOperation = DataPackageOperation.None; ViewModel.InputFeedback = "无法加入或移动这些项目，请重新选择目标文件夹。"; }
        finally { currentDrag = null; deferral.Complete(); }
    }
}
