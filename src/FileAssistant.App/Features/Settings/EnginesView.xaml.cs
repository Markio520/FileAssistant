using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Settings;

public sealed partial class EnginesView : UserControl
{
    public EnginesViewModel ViewModel => App.EngineSettings;
    public EnginesView() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null); }
}
