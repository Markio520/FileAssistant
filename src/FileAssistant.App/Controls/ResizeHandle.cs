using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FileAssistant.App.Controls;

/// <summary>Shared horizontal drag and keyboard input for table columns and side panes.</summary>
public sealed class ResizeHandle : UserControl
{
    public event EventHandler<double>? ResizeRequested;

    public ResizeHandle()
    {
        var thumb = new Thumb { Style = (Style)Application.Current.Resources["ResizeThumbStyle"] };
        Content = thumb;
        Style = (Style)Application.Current.Resources["ResizeHandleStyle"];
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        Loaded += (_, _) => AutomationProperties.SetName(thumb, AutomationProperties.GetName(this));
        thumb.DragStarted += (_, _) => thumb.Focus(FocusState.Pointer);
        thumb.DragDelta += (_, e) => ResizeRequested?.Invoke(this, e.HorizontalChange);
        thumb.KeyDown += Handle_KeyDown;
    }

    private void Handle_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
        ResizeRequested?.Invoke(this, e.Key == VirtualKey.Left ? -8 : 8);
        e.Handled = true;
    }
}
