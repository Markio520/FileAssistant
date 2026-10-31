using Microsoft.UI.Xaml.Controls;
using FileAssistant.App.Features.FileInput;

namespace FileAssistant.App.Features.Convert;

public sealed partial class ConversionView : UserControl
{
    public ConversionViewModel ViewModel => App.Conversion;
    public event EventHandler? SearchRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForCopyRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForConversionRequested;
    public ConversionView()
    {
        InitializeComponent();
        ViewModel.RunCommand.PropertyChanged += (_, _) => { if (ViewModel.RunCommand.IsRunning) App.Workspace.IsQueueExpanded = true; };
    }
    public void FocusQuery() => InputView.FocusQuery();
    private void Input_SearchRequested(object? sender, EventArgs e) => SearchRequested?.Invoke(this,e);
    private void Input_FilesForCopyRequested(object? sender, FileInputTransferEventArgs e) => FilesForCopyRequested?.Invoke(this, e);
    private void Input_FilesForConversionRequested(object? sender, FileInputTransferEventArgs e) => FilesForConversionRequested?.Invoke(this, e);
}
