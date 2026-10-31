using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.Search;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace FileAssistant.App.Features.FileInput;

public sealed class FileInputTransferEventArgs(IReadOnlyList<SearchFile> files) : EventArgs
{
    public IReadOnlyList<SearchFile> Files { get; } = files;
}

public sealed partial class FileInputView : UserControl
{
    private bool choosing;
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WorkspaceViewModel), typeof(FileInputView),
        new PropertyMetadata(null, (owner, _) => { var view = (FileInputView)owner; view.Bindings.Update(); view.UpdateInspector(); }));
    public WorkspaceViewModel ViewModel
    {
        get => (WorkspaceViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public WorkspaceViewModel TaskViewModel => App.Workspace;
    public static readonly DependencyProperty OperationContentProperty = DependencyProperty.Register(nameof(OperationContent), typeof(UIElement),
        typeof(FileInputView), new PropertyMetadata(null, (owner, _) => ((FileInputView)owner).UpdateInspector()));
    public UIElement? OperationContent { get => (UIElement?)GetValue(OperationContentProperty); set => SetValue(OperationContentProperty, value); }
    public event EventHandler? SearchRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForCopyRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForConversionRequested;

    public FileInputView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateInspector();
        KeyDown += Input_KeyDown;
    }

    private void UpdateInspector()
    {
        if (IsLoaded && ViewModel is not null) Inspector.ShowParameters(ViewModel.IsFunctionInput, OperationContent);
    }

    public void FocusQuery() => SearchBox.Focus(FocusState.Programmatic);
    private FileInputColumnWidths ColumnWidths => (FileInputColumnWidths)Resources["FileColumns"];
    private void FilesViewport_SizeChanged(object sender, SizeChangedEventArgs e) => ColumnWidths.FitViewport(e.NewSize.Width);
    private void FilesHeader_SizeChanged(object sender, SizeChangedEventArgs e) => FilesHeaderRow.Height = new GridLength(e.NewSize.Height);
    private void Column_ResizeRequested(object? sender, double delta)
        => ColumnWidths.Resize((string)((FrameworkElement)sender!).Tag, delta);

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) ViewModel.SearchText = sender.Text;
    }

    private async void PickFiles_Click(object sender, RoutedEventArgs e) => await ChooseAsync(FileInputSources.PickFilesAsync);
    private async void PickFolder_Click(object sender, RoutedEventArgs e) => await ChooseAsync(FileInputSources.PickFolderAsync);
    private async Task ChooseAsync(Func<Task<IReadOnlyList<string>>> pick)
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        choosing = true;
        var target = ViewModel;
        try
        {
            var paths = await pick();
            if (paths.Count > 0) await target.ImportPathsAsync(paths);
        }
        catch (Exception) { target.InputFeedback = "无法打开文件选择器，请重试，或直接拖入文件。"; }
        finally { choosing = false; }
    }

    private async Task PasteAsync()
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        var target = ViewModel;
        try
        {
            var paths = await FileInputSources.ReadAsync(Clipboard.GetContent());
            if (paths.Count == 0) target.InputFeedback = "剪贴板中没有可添加的文件。请复制文件，或每行一个完整文件路径。";
            else await target.ImportPathsAsync(paths);
        }
        catch (Exception) { target.InputFeedback = "无法读取剪贴板，请重新复制文件后重试。"; }
    }

    private async void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.V || Visibility != Visibility.Visible ||
            !InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)) return;
        // Text inputs keep their ordinary paste behavior.
        for (var focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; focus is not null; focus = VisualTreeHelper.GetParent(focus))
            if (focus is TextBox or RichEditBox or PasswordBox) return;
        e.Handled = true;
        await PasteAsync();
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = ViewModel.CanReceiveFiles && !choosing && FileInputSources.CanRead(e.DataView)
            ? DataPackageOperation.Copy : DataPackageOperation.None;
        if (e.AcceptedOperation != DataPackageOperation.None)
        {
            e.DragUIOverride.Caption = ViewModel.IsStaging ? "加入暂存区" : "加入此功能的输入列表";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        e.Handled = true;
    }

    private async void Files_Drop(object sender, DragEventArgs e)
    {
        if (!ViewModel.CanReceiveFiles || choosing) { e.AcceptedOperation = DataPackageOperation.None; return; }
        var deferral = e.GetDeferral();
        var target = ViewModel;
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.Handled = true;
        try
        {
            var paths = await FileInputSources.ReadAsync(e.DataView);
            if (paths.Count == 0) target.InputFeedback = "未发现可添加的文件，请拖入本地文件或文件夹。";
            else await target.ImportPathsAsync(paths);
        }
        catch (Exception) { target.InputFeedback = "无法读取拖入的项目，请通过“添加”选择文件。"; }
        finally { deferral.Complete(); }
    }

    private IReadOnlyList<SearchFile> SelectedFiles() => FileList.SelectedItems.OfType<WorkspaceFile>()
        .Where(file => file.Source is not null).Select(file => file.Source!).ToArray();
    private void SendSelected_Click(object sender, RoutedEventArgs e) => SendToCopy(SelectedFiles());
    private void SendAll_Click(object sender, RoutedEventArgs e) => SendToCopy(ViewModel.SearchEntries);
    private void ConvertSelected_Click(object sender, RoutedEventArgs e) => SendToConversion(SelectedFiles());
    private void ConvertAll_Click(object sender, RoutedEventArgs e) => SendToConversion(ViewModel.SearchEntries);
    private void SendToConversion(IReadOnlyList<SearchFile> files)
    { if (files.Count > 0 && ViewModel.CanReceiveFiles) FilesForConversionRequested?.Invoke(this, new(files)); }
    private void SendToCopy(IReadOnlyList<SearchFile> files)
    {
        if (files.Count > 0 && ViewModel.CanReceiveFiles) FilesForCopyRequested?.Invoke(this, new(files));
    }

    private async void AddToStaging_Click(object sender, RoutedEventArgs e)
    {
        var files = SelectedFiles();
        if (files.Count == 0 || !ViewModel.CanExportSelection) return;
        var report = await App.Workspace.ImportFilesAsync(files);
        ViewModel.ImportReport = report;
        ViewModel.InputFeedback = $"加入暂存区：{report.Summary}";
    }

    private async void ImportFromStaging_Click(object sender, RoutedEventArgs e)
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        var files = App.Workspace.SearchEntries.Select(WorkspaceFile.FromSource).ToArray();
        if (files.Length == 0) { ViewModel.InputFeedback = "暂存区没有可导入的文件。也可以通过“添加”、拖入或粘贴直接添加。"; return; }
        choosing = true;
        try
        {
            var list = new ListView { ItemsSource = files, SelectionMode = ListViewSelectionMode.Multiple,
                IsMultiSelectCheckBoxEnabled = true, ItemTemplate = (DataTemplate)Resources["StagingItemTemplate"], MaxHeight = 360 };
            var all = new Button { Content = $"选择全部 {files.Length:N0} 项" };
            all.Click += (_, _) => list.SelectAll();
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "选择要导入的文件。追加到当前列表，重复路径会跳过。", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(all); panel.Children.Add(list);
            var dialog = new ContentDialog { Title = "从暂存区导入", Content = panel, XamlRoot = XamlRoot, RequestedTheme = ActualTheme,
                PrimaryButtonText = "导入选中项（0）", CloseButtonText = "取消", IsPrimaryButtonEnabled = false, DefaultButton = ContentDialogButton.Primary };
            list.SelectionChanged += (_, _) => { dialog.IsPrimaryButtonEnabled = list.SelectedItems.Count > 0;
                dialog.PrimaryButtonText = $"导入选中项（{list.SelectedItems.Count:N0}）"; };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await ViewModel.ImportFilesAsync(list.SelectedItems.Cast<WorkspaceFile>().Select(file => file.Source!).ToArray());
        }
        catch (Exception) { ViewModel.InputFeedback = "暂时无法打开导入窗口，请关闭其他弹窗后重试。"; }
        finally { choosing = false; }
    }

    private async void ImportDetails_Click(object sender, RoutedEventArgs e)
    {
        if (choosing || !ViewModel.HasImportDetails) return;
        choosing = true;
        try
        {
            await new ContentDialog { Title = "添加结果明细", XamlRoot = XamlRoot, RequestedTheme = ActualTheme, CloseButtonText = "关闭",
                Content = new ListView { ItemsSource = ViewModel.ImportReport.Items, SelectionMode = ListViewSelectionMode.None,
                    MaxHeight = 420, ItemTemplate = (DataTemplate)Resources["ImportResultTemplate"] } }.ShowAsync();
        }
        catch (Exception) { ViewModel.InputFeedback = "暂时无法打开明细，请关闭其他弹窗后重试。"; }
        finally { choosing = false; }
    }

    private void FindFiles_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);
    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectionCount = FileList.SelectedItems.Count;
        ViewModel.SelectedFile = e.AddedItems.OfType<WorkspaceFile>().LastOrDefault() ?? FileList.SelectedItems.OfType<WorkspaceFile>().FirstOrDefault();
    }
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelection();
    private void Remove_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { RemoveSelection(); args.Handled = true; }
    private void SelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { FileList.SelectAll(); args.Handled = true; }
    private void RemoveSelection()
    {
        if (ViewModel.CanEditList) ViewModel.RemoveFromList(FileList.SelectedItems.OfType<WorkspaceFile>().ToArray());
    }
    private void WindowSize_Click(object sender, RoutedEventArgs e)
    {
        if (App.Window is MainWindow window && sender is MenuFlyoutItem item) window.SetWorkspaceSize(item.Tag is "compact");
    }
}
