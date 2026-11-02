using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace FileAssistant.App.Controls;

/// <summary>Shared file/folder entry point; the owning feature supplies the import operation.</summary>
public sealed class AddFilesButton : Button
{
    public event RoutedEventHandler? FilesRequested;
    public event RoutedEventHandler? FolderRequested;

    public AddFilesButton()
    {
        Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        AutomationProperties.SetName(this, "添加文件或文件夹");
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        label.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 12 });
        label.Children.Add(new TextBlock { Text = "添加文件或文件夹", FontSize = 12 });
        Content = label;
        var menu = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.Bottom,
            MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["ActionDropDownPresenterStyle"]
        };
        var files = new MenuFlyoutItem { Text = "添加文件…", Style = (Style)Application.Current.Resources["ActionDropDownItemStyle"] };
        var folder = new MenuFlyoutItem { Text = "添加文件夹…", Style = files.Style };
        files.Click += (_, args) => FilesRequested?.Invoke(this, args);
        folder.Click += (_, args) => FolderRequested?.Invoke(this, args);
        menu.Items.Add(files); menu.Items.Add(folder);
        Flyout = menu;
    }
}
