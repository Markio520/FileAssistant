using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace FileAssistant.App.Controls;

/// <summary>Action menu sharing the selection dropdown's button, popup and row metrics.</summary>
public sealed class ActionDropDown : DropDownButton
{
    private MenuFlyout? actionMenu;
    private FocusState openingFocusState;

    public ActionDropDown()
    {
        Style = (Style)Application.Current.Resources["SelectionDropDownButtonStyle"];
        RegisterPropertyChangedCallback(FlyoutProperty, (_, _) => ConfigureFlyout());
    }

    private void ConfigureFlyout()
    {
        if (actionMenu is not null)
        {
            actionMenu.Opening -= Menu_Opening;
            actionMenu.Opened -= Menu_Opened;
        }
        actionMenu = Flyout as MenuFlyout;
        if (actionMenu is not { } menu) return;
        menu.Placement = FlyoutPlacementMode.Bottom;
        menu.MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["ActionDropDownPresenterStyle"];
        var itemStyle = (Style)Application.Current.Resources["ActionDropDownItemStyle"];
        foreach (var item in menu.Items.OfType<MenuFlyoutItem>()) item.Style = itemStyle;
        menu.Opening += Menu_Opening;
        menu.Opened += Menu_Opened;
    }

    private void Menu_Opening(object? sender, object e)
        => openingFocusState = FocusState == FocusState.Keyboard ? FocusState.Keyboard : FocusState.Programmatic;

    private void Menu_Opened(object? sender, object e)
        => actionMenu?.Items.OfType<MenuFlyoutItem>()
            .FirstOrDefault(item => item.IsEnabled && item.Visibility == Visibility.Visible)?.Focus(openingFocusState);
}
