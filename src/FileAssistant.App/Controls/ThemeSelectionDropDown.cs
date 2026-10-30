using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Controls;

/// <summary>Application appearance selector using the shared single-choice menu.</summary>
public sealed class ThemeSelectionDropDown : SelectionDropDown
{
    private FrameworkElement? themeRoot;
    private long themeCallbackToken;

    public ThemeSelectionDropDown()
    {
        ItemsSource = new SelectionOption[]
        {
            new((int)ElementTheme.Default, "跟随系统"),
            new((int)ElementTheme.Light, "浅色"),
            new((int)ElementTheme.Dark, "深色")
        };
        ButtonContent = new FontIcon { Glyph = "\uE790", FontSize = 16 };
        MinWidth = 0;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        ToolTipService.SetToolTip(this, "切换应用外观");
        AutomationProperties.SetName(this, "切换应用外观");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SelectionCommitted += (_, _) =>
        {
            if (themeRoot is not null) themeRoot.RequestedTheme = (ElementTheme)SelectedValue;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (themeRoot is not null || App.Window.Content is not FrameworkElement root) return;
        themeRoot = root;
        SelectedValue = (int)root.RequestedTheme;
        themeCallbackToken = root.RegisterPropertyChangedCallback(RequestedThemeProperty,
            (_, _) => SelectedValue = (int)root.RequestedTheme);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        themeRoot?.UnregisterPropertyChangedCallback(RequestedThemeProperty, themeCallbackToken);
        themeRoot = null;
    }
}
