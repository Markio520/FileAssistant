using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FileAssistant.App.Features.Workspace;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class WorkspacePage : Page
{
    public WorkspaceViewModel ViewModel { get; } = new();

    public WorkspacePage()
    {
        InitializeComponent();
    }
}
