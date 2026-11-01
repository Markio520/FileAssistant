using FileAssistant.App.Features.Workspace;
using FileAssistant.Core.Features.Search;
using FileAssistant.Core.Features.FileCollections;
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
    public FileCollectionBundle Bundle { get; private init; } = FileCollectionBundle.FromFiles(files);
    public FileInputTransferEventArgs(FileCollectionBundle bundle) : this(bundle.Files) { Bundle = bundle; }
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
        // Item containers handle selection gestures; navigation must also receive handled double taps.
        FileList.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(Files_DoubleTapped), handledEventsToo: true);
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
        var destination = target.CurrentFolderId;
        try
        {
            var paths = await pick();
            if (paths.Count > 0) await target.ImportPathsAsync(paths, destination);
        }
        catch (Exception) { target.InputFeedback = "无法打开文件选择器，请重试，或直接拖入文件。"; }
        finally { choosing = false; }
    }

    private async Task PasteAsync()
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        var target = ViewModel;
        var destination = target.CurrentFolderId;
        try
        {
            var paths = await FileInputSources.ReadAsync(Clipboard.GetContent());
            if (paths.Count == 0) target.InputFeedback = "剪贴板中没有可添加的文件。请复制文件，或每行一个完整文件路径。";
            else await target.ImportPathsAsync(paths, destination);
        }
        catch (Exception) { target.InputFeedback = "无法读取剪贴板，请重新复制文件后重试。"; }
    }

    private async void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.V or VirtualKey.Z) || Visibility != Visibility.Visible ||
            !InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)) return;
        // Text inputs keep their ordinary paste and undo behavior.
        for (var focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; focus is not null; focus = VisualTreeHelper.GetParent(focus))
            if (focus is TextBox or RichEditBox or PasswordBox) return;
        e.Handled = true;
        if (e.Key == VirtualKey.Z) { if (!choosing && ViewModel.CanUndo) ViewModel.UndoCommand.Execute(null); }
        else await PasteAsync();
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        ClearDropHighlight();
        CollectionDragOver(e, ViewModel.CurrentFolderId);
    }

    private async void Files_Drop(object sender, DragEventArgs e)
    {
        await CollectionDropAsync(e, ViewModel.CurrentFolderId);
    }

    private void SendSelected_Click(object sender, RoutedEventArgs e) => SendToCopy(ViewModel.ExportSelection());
    private void SendAll_Click(object sender, RoutedEventArgs e) => SendToCopy(ViewModel.ExportCurrentFolder());
    private void ConvertSelected_Click(object sender, RoutedEventArgs e) => SendToConversion(ViewModel.ExportSelection());
    private void ConvertAll_Click(object sender, RoutedEventArgs e) => SendToConversion(ViewModel.ExportCurrentFolder());
    private void SendToConversion(FileCollectionBundle bundle)
    { if (bundle.HasItems && ViewModel.CanReceiveFiles) FilesForConversionRequested?.Invoke(this, new(bundle)); }
    private void SendToCopy(FileCollectionBundle bundle)
    {
        if (bundle.HasItems && ViewModel.CanReceiveFiles) FilesForCopyRequested?.Invoke(this, new(bundle));
    }

    private async void AddToStaging_Click(object sender, RoutedEventArgs e)
    {
        if (choosing || !ViewModel.CanExportSelection) return;
        choosing = true;
        try
        {
            var report = await FileCollectionDialogs.ImportAsync(App.Workspace, ViewModel.ExportSelection(), this);
            if (report is not null) { ViewModel.ImportReport = report; ViewModel.InputFeedback = $"加入暂存区：{report.Summary}"; }
        }
        catch (Exception) { ViewModel.InputFeedback = "无法打开目标文件夹选择窗口，请稍后重试。"; }
        finally { choosing = false; }
    }

    private async void ImportFromStaging_Click(object sender, RoutedEventArgs e)
    {
        if (choosing || !ViewModel.CanReceiveFiles) return;
        if (!App.Workspace.HasFiles) { ViewModel.InputFeedback = "暂存区没有可导入的文件或软件文件夹。"; return; }
        if (App.Workspace.HasSamples) { ViewModel.InputFeedback = "示例文件不能导入，请先添加真实文件。"; return; }
        choosing = true;
        try
        {
            await FileCollectionDialogs.ImportFromAsync(App.Workspace, ViewModel, this);
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
        ViewModel.SetSelection(FileList.SelectedItems.OfType<WorkspaceFile>().ToArray(),
            e.AddedItems.OfType<WorkspaceFile>().LastOrDefault() ?? FileList.SelectedItems.OfType<WorkspaceFile>().FirstOrDefault());
    }
    private async void Remove_Click(object sender, RoutedEventArgs e) => await RemoveSelectionAsync();
    private async void Remove_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { args.Handled = true; await RemoveSelectionAsync(); }
    private void SelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { FileList.SelectAll(); args.Handled = true; }
    private async Task RemoveSelectionAsync()
    {
        if (!ViewModel.CanEditList || choosing) return;
        var items = FileList.SelectedItems.OfType<WorkspaceFile>().ToArray();
        if (items.Length == 0) return;
        choosing = true;
        try
        {
            if (items.Any(item => item.IsCollectionFolder))
            {
                var count = ViewModel.ExportItems(items).Files.Count;
                var dialog = FileCollectionDialogs.Dialog(this, "从列表移除文件夹及内容？",
                    new TextBlock { Text = $"将移出所选文件夹及其中 {count:N0} 项。原文件和其他功能中的列表保留，可撤销本次操作。", TextWrapping = TextWrapping.Wrap }, "从列表移除");
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            ViewModel.RemoveFromList(items);
        }
        catch (Exception) { ViewModel.InputFeedback = "无法移出这些项目，请刷新列表后重试。"; }
        finally { choosing = false; }
    }
    private void WindowSize_Click(object sender, RoutedEventArgs e)
    {
        if (App.Window is MainWindow window && sender is MenuFlyoutItem item) window.SetWorkspaceSize(item.Tag is "compact");
    }
}
