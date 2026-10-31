using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.FileCollections;
using FileAssistant.Core.Features.FileInput;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.FileInput;

/// <summary>Shared destination and naming dialogs for input, search and output transfers.</summary>
public static class FileCollectionDialogs
{
    public static async Task<FileImportReport?> ImportAsync(WorkspaceViewModel target, FileCollectionBundle bundle, FrameworkElement owner)
    {
        if (!bundle.HasItems || !target.CanReceiveFiles) return null;
        var picker = new CollectionFolderPicker(); picker.Initialize(target);
        var preserve = PreservationOption(bundle);
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = BundleSummary(bundle), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(picker); panel.Children.Add(preserve);
        panel.Children.Add(TransferNote());
        var dialog = Dialog(owner, "加入" + target.Title, panel, JoinLabel(bundle));
        void Update() => dialog.IsPrimaryButtonEnabled = picker.SelectedFolderId is not null && CanImport(bundle, preserve);
        picker.SelectionChanged += (_, _) => Update();
        preserve.Checked += (_, _) => Update(); preserve.Unchecked += (_, _) => Update();
        Update();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || picker.SelectedFolderId is not { } folder) return null;
        return await ImportCheckedAsync(target, bundle, folder, preserve.IsChecked == true);
    }
    public static async Task<FileImportReport?> ImportFromAsync(WorkspaceViewModel source, WorkspaceViewModel target, FrameworkElement owner)
    {
        if (!target.CanReceiveFiles || !source.CanReceiveFiles) return null;
        var destination = new CollectionFolderPicker(); destination.Initialize(target);
        var picker = new CollectionSourcePicker(); picker.Initialize(source);
        var preserve = PreservationOption(new([], []));
        // Keep rows stationary between the two clicks when folder selection reveals this option.
        var preserveSlot = new Grid { MinHeight = 32 }; preserveSlot.Children.Add(preserve);
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(destination); panel.Children.Add(picker); panel.Children.Add(preserveSlot); panel.Children.Add(TransferNote());
        var dialog = Dialog(owner, "从暂存区加入" + target.Title, panel, "选择要加入的项目");
        dialog.IsPrimaryButtonEnabled = false;
        void Update()
        {
            var bundle = picker.Selection;
            dialog.IsPrimaryButtonEnabled = picker.HasSelection && destination.SelectedFolderId is not null && CanImport(bundle, preserve);
            dialog.PrimaryButtonText = picker.HasSelection ? JoinLabel(bundle) : "选择要加入的项目";
            preserve.Visibility = bundle.Folders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        picker.SelectionChanged += (_, _) => Update(); destination.SelectionChanged += (_, _) => Update();
        preserve.Checked += (_, _) => Update(); preserve.Unchecked += (_, _) => Update();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || destination.SelectedFolderId is not { } folder) return null;
        return await ImportCheckedAsync(target, picker.Selection, folder, preserve.IsChecked == true);
    }
    private static string JoinLabel(FileCollectionBundle bundle) => bundle.Files.Count > 0 ? $"加入 {bundle.Files.Count:N0} 项" : "加入文件夹";
    private static bool CanImport(FileCollectionBundle bundle, CheckBox preserve) => bundle.Files.Count > 0 || preserve.IsChecked == true && bundle.Folders.Count > 0;
    private static string BundleSummary(FileCollectionBundle bundle) => bundle.Folders.Count == 0 ? $"已选择 {bundle.Files.Count:N0} 项"
        : $"已选择文件夹及内容 · 实际 {bundle.Files.Count:N0} 项";
    private static CheckBox PreservationOption(FileCollectionBundle bundle) => new() { Content = "保留文件夹结构", IsChecked = true, Visibility = bundle.Folders.Count > 0 ? Visibility.Visible : Visibility.Collapsed };
    private static TextBlock TransferNote() => new() { Text = "原文件位置不变，重复项自动跳过。", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private static async Task<FileImportReport> ImportCheckedAsync(WorkspaceViewModel target, FileCollectionBundle bundle, Guid folder, bool preserve)
    {
        try { return await target.ImportBundleAsync(bundle, folder, preserve); }
        catch (ArgumentException error) { target.InputFeedback = error.Message; return new([], error.Message); }
        catch (Exception) { target.InputFeedback = "加入失败，请检查文件和目标文件夹后重试。"; return new([], target.InputFeedback); }
    }
    public static async Task RenameOrCreateAsync(WorkspaceViewModel target, FrameworkElement owner, Guid? id = null)
    {
        var name = new TextBox { Header = "文件夹名称", Text = id is { } folder ? target.FolderName(folder) : "新建文件夹", MaxLength = 120 };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(name); panel.Children.Add(error);
        var dialog = Dialog(owner, id is null ? "新建文件夹" : "重命名文件夹", panel, id is null ? "创建" : "保存");
        dialog.Opened += (_, _) => { name.Focus(FocusState.Programmatic); name.SelectAll(); };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try { if (id is { } existing) target.RenameFolder(existing, name.Text); else target.CreateFolder(name.Text); }
            catch (ArgumentException issue) { error.Text = issue.Message; args.Cancel = true; }
        };
        await dialog.ShowAsync();
    }
    public static async Task MoveAsync(WorkspaceViewModel target, IReadOnlyList<WorkspaceFile> items, FrameworkElement owner, bool alsoAdd = false)
    {
        var folders = items.Where(item => item.FolderId is not null).Select(item => item.FolderId!.Value).ToArray();
        var bundle = target.ExportItems(items);
        var picker = new CollectionFolderPicker(); picker.Initialize(target, alsoAdd ? null : id => target.CanMoveTo(folders, id), compact: false);
        var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(picker);
        panel.Children.Add(new TextBlock { Text = alsoAdd ? "加入后，原文件夹中的项目保留。原文件位置不变。" : "仅在应用内移动，原文件位置不变。", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var dialog = Dialog(owner, alsoAdd ? "添加到其他文件夹" : "移动到文件夹", panel, alsoAdd ? "加入" : "移动");
        dialog.IsPrimaryButtonEnabled = picker.SelectedFolderId is not null;
        picker.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = picker.SelectedFolderId is not null;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || picker.SelectedFolderId is not { } folder) return;
        if (alsoAdd) await target.ImportBundleAsync(bundle, folder, preserveFolders: true); else target.MoveItemsTo(items, folder);
    }
    public static ContentDialog Dialog(FrameworkElement owner, string title, object content, string primary) => new()
    {
        Title = title, Content = content, XamlRoot = owner.XamlRoot, RequestedTheme = owner.ActualTheme,
        PrimaryButtonText = primary, CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary
    };
}
