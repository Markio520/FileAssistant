using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Convert;

public sealed partial class ConversionView : UserControl
{
    public ConversionViewModel ViewModel => App.Conversion;
    public event EventHandler? SearchRequested;
    public ConversionView()
    {
        InitializeComponent();
        ViewModel.RunCommand.PropertyChanged += (_, _) => { if (ViewModel.RunCommand.IsRunning) App.Workspace.IsQueueExpanded = true; };
    }
    public void FocusQuery() => InputView.FocusQuery();
    private void Input_SearchRequested(object? sender, EventArgs e) => SearchRequested?.Invoke(this,e);
}
