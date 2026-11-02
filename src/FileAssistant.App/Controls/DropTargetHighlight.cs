using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Controls;

/// <summary>Temporary drop feedback that restores the target's original local value or style.</summary>
public sealed class DropTargetHighlight
{
    private DependencyObject? target;
    private DependencyProperty? backgroundProperty;
    private object? previousValue;

    public void Show(DependencyObject next)
    {
        if (ReferenceEquals(target, next)) return;
        Clear();
        backgroundProperty = next switch
        {
            Panel => Panel.BackgroundProperty,
            Control => Control.BackgroundProperty,
            Border => Border.BackgroundProperty,
            _ => null
        };
        if (backgroundProperty is null) return;
        target = next;
        previousValue = target.ReadLocalValue(backgroundProperty);
        target.SetValue(backgroundProperty, Application.Current.Resources["WorkspaceAccentSubtleBrush"]);
    }

    public void Clear()
    {
        if (target is not null && backgroundProperty is not null)
        {
            if (ReferenceEquals(previousValue, DependencyProperty.UnsetValue)) target.ClearValue(backgroundProperty);
            else target.SetValue(backgroundProperty, previousValue);
        }
        target = null; backgroundProperty = null; previousValue = null;
    }
}
