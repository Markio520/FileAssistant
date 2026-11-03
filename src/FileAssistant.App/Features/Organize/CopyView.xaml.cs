using FileAssistant.App.Features.FileInput;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Organize;

public sealed partial class CopyView : UserControl
{
    public CopyViewModel ViewModel => App.Copy;
    public event EventHandler? SearchRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForCopyRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForConversionRequested;
    public CopyView()
    {
        InitializeComponent();
        ViewModel.RunCommand.PropertyChanged += (_, _) => { if (ViewModel.RunCommand.IsRunning) App.Workspace.IsQueueExpanded = true; };
    }
    public void FocusQuery() => InputView.FocusQuery();
    public void SetActive(bool active) => OptionsPanel.SetActive(active);
    private void Input_SearchRequested(object? sender, EventArgs e) => SearchRequested?.Invoke(this, e);
    private void Input_FilesForCopyRequested(object? sender, FileInputTransferEventArgs e) => FilesForCopyRequested?.Invoke(this, e);
    private void Input_FilesForConversionRequested(object? sender, FileInputTransferEventArgs e) => FilesForConversionRequested?.Invoke(this, e);
}
