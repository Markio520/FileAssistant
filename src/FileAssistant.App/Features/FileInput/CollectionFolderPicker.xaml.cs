using FileAssistant.App.Features.Workspace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class CollectionFolderPicker : UserControl
{
    private WorkspaceViewModel target = null!;
    private Func<Guid, bool> allowed = _ => true;
    public Guid? SelectedFolderId => (Folders.SelectedNode?.Content as FolderBreadcrumb)?.Id;
    public event EventHandler? SelectionChanged;
    public CollectionFolderPicker() => InitializeComponent();
    public void Initialize(WorkspaceViewModel target, Func<Guid, bool>? allowed = null, bool compact = true)
    {
        this.target = target; this.allowed = allowed ?? (_ => true); Refresh(target.CurrentFolderId);
        TreeRegion.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ChangeButton.Content = compact ? "更改" : "收起";
    }
    private void Refresh(Guid selected)
    {
        var root = new TreeViewNode { Content = new FolderBreadcrumb(Guid.Empty, target.Title), IsExpanded = true };
        var nodes = new Dictionary<Guid, TreeViewNode> { [Guid.Empty] = root };
        var folders = target.CollectionSnapshot.Folders.Where(folder => allowed(folder.Id)).ToArray();
        foreach (var folder in folders) nodes[folder.Id] = new() { Content = new FolderBreadcrumb(folder.Id, folder.Name) };
        foreach (var folder in folders) if (nodes.TryGetValue(folder.ParentId, out var parent)) parent.Children.Add(nodes[folder.Id]);
        Folders.RootNodes.Clear(); Folders.RootNodes.Add(root);
        var current = nodes.GetValueOrDefault(selected) ?? root;
        for (var parent = current.Parent; parent is not null; parent = parent.Parent) parent.IsExpanded = true;
        Folders.SelectedNode = current;
        UpdatePath();
    }
    private void UpdatePath()
    {
        if (target is null) return;
        TargetPath.Text = target.FolderChoices.FirstOrDefault(folder => folder.Id == SelectedFolderId)?.Label ?? "请选择目标文件夹";
        NewName.Header = "新建到：" + TargetPath.Text;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Folders_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e) => UpdatePath();
    private void Change_Click(object sender, RoutedEventArgs e)
    {
        var show = TreeRegion.Visibility != Visibility.Visible;
        TreeRegion.Visibility = show ? Visibility.Visible : Visibility.Collapsed; ChangeButton.Content = show ? "收起" : "更改";
    }
    private void NewFolder_Click(object sender, RoutedEventArgs e)
    { NameEditor.Visibility = Visibility.Visible; Message.Text = string.Empty; NewName.Focus(FocusState.Programmatic); }
    private void CancelNew_Click(object sender, RoutedEventArgs e) { NameEditor.Visibility = Visibility.Collapsed; NewName.Text = string.Empty; }
    private void NewName_KeyDown(object sender, KeyRoutedEventArgs e)
    { if (e.Key == VirtualKey.Enter) { e.Handled = true; Create_Click(sender, e); } }
    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolderId is not { } parent || !target.CanReceiveFiles) return;
        try { var id = target.CreateFolder(NewName.Text, parent); NewName.Text = string.Empty; NameEditor.Visibility = Visibility.Collapsed; Refresh(id); Message.Text = "已选择新建文件夹。"; }
        catch (ArgumentException error) { Message.Text = error.Message; }
    }
}
