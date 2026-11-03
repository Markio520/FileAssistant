using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileAssistant.App.Controls;

public interface IDropTargetHandler
{
    void TargetDragOver(DependencyObject source, DragEventArgs args);
    void TargetDrop(DependencyObject source, DragEventArgs args);
    void TargetDragLeave(DependencyObject source, DragEventArgs args);
}

/// <summary>Forwards whole-row drops before a native item container can reorder itself.</summary>
public sealed class DropTargetSurface : Grid
{
    public DropTargetSurface()
    {
        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        Drop += OnDrop;
        DragLeave += OnDragLeave;
    }

    private IDropTargetHandler? FindHandler()
    {
        for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is IDropTargetHandler handler) return handler;
        return null;
    }

    // Forward directly: handled drag events do not reliably reach an ancestor's
    // AddHandler subscription in the shipped runtime. The native TreeViewItem must
    // still see Handled=true so it cannot move visual nodes without updating data.
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (FindHandler() is not { } handler) return;
        e.Handled = true;
        handler.TargetDragOver(this, e);
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (FindHandler() is not { } handler) return;
        e.Handled = true;
        handler.TargetDrop(this, e);
    }
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (FindHandler() is not { } handler) return;
        e.Handled = true;
        handler.TargetDragLeave(this, e);
    }
}
