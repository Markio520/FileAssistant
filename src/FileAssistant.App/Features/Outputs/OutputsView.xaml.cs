using FileAssistant.App.Features.FileInput;
using FileAssistant.App.Features.Workspace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.Outputs;

public sealed partial class OutputsView : UserControl
{
    public OutputsViewModel ViewModel => App.Outputs;
    public WorkspaceViewModel QueueViewModel => App.Workspace;
    public event EventHandler<FileInputTransferEventArgs>? FilesForConversionRequested;
    public event EventHandler<FileInputTransferEventArgs>? FilesForCopyRequested;
    public OutputsView() { InitializeComponent(); Loaded += (_, _) => Inspector.ShowParameters(false); }
    public void FocusQuery() => Query.Focus(FocusState.Programmatic);
    private void Query_TextChanged(object sender, TextChangedEventArgs e) => ViewModel.SearchText = Query.Text;
    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e) => ViewModel.SetSelection(Files.SelectedItems.Cast<OutputEntry>().ToArray());
    private void Files_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ViewModel.OpenCommand.Execute(null);
    private async void AddToStaging_Click(object sender, RoutedEventArgs e)
    {
        var report = await FileCollectionDialogs.ImportAsync(App.Workspace, Core.Features.FileCollections.FileCollectionBundle.FromFiles(ViewModel.SelectedFiles), this);
        if (report is not null) ViewModel.Feedback = report.Summary;
    }
    private void Convert_Click(object sender, RoutedEventArgs e) => FilesForConversionRequested?.Invoke(this,new(ViewModel.SelectedFiles));
    private void Copy_Click(object sender, RoutedEventArgs e) => FilesForCopyRequested?.Invoke(this,new(ViewModel.SelectedFiles));
}
