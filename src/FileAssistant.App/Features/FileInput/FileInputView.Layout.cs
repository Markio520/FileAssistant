using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class FileInputView
{
    private const double MinimumFoldersWidth = 176;
    private const double MaximumFoldersWidth = 360;
    private const double MinimumFilesWidth = 360;
    private double preferredFoldersWidth = MinimumFoldersWidth;

    private double AvailableFoldersWidth => Math.Clamp(InputSurface.ActualWidth - MinimumFilesWidth, MinimumFoldersWidth, MaximumFoldersWidth);
    private void InputSurface_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFoldersWidth();
    private void UpdateFoldersWidth()
    {
        if (FoldersResizeHandle is null) return;
        var open = FolderSidebar.Visibility == Visibility.Visible;
        FoldersColumn.Width = new GridLength(open ? Math.Clamp(preferredFoldersWidth, MinimumFoldersWidth, AvailableFoldersWidth) : 48);
        // Reserve the user's chosen width when resizing the right pane, but allow a
        // small window to constrain the left pane without discarding that preference.
        DetailsHost.MinimumContentWidth = MinimumFilesWidth + (open ? preferredFoldersWidth : 48);
    }

    private void Folders_ResizeRequested(object? sender, double delta)
    {
        if (FolderSidebar.Visibility != Visibility.Visible || !double.IsFinite(delta)) return;
        preferredFoldersWidth = Math.Clamp(FoldersColumn.ActualWidth + delta, MinimumFoldersWidth, AvailableFoldersWidth);
        UpdateFoldersWidth();
    }

    private double? preferredListHeight;
    private double AvailableListHeight => Math.Max(0, WorkspaceLayout.ActualHeight - WorkspaceHeader.ActualHeight - WorkspaceLayout.RowSpacing - 12);
    private double ConstrainListHeight(double height) => Math.Clamp(height, Math.Min(320, AvailableListHeight), AvailableListHeight);

    private void ListHeightHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateListHeight();
    private void WorkspaceLayout_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateListHeight();
    private void UpdateListHeight()
    {
        // Keep the default row flexible so a smaller window can remeasure the entire surface.
        var height = preferredListHeight is { } preferred ? ConstrainListHeight(preferred) : AvailableListHeight;
        ListHeightRow.Height = new GridLength(Math.Max(1, height), GridUnitType.Star);
        ListRemainderRow.Height = new GridLength(Math.Max(0, AvailableListHeight - height), GridUnitType.Star);
    }

    private void ListHeight_ResizeRequested(object? sender, double delta)
    {
        if (!double.IsFinite(delta)) return;
        preferredListHeight = ConstrainListHeight(ConstrainListHeight(preferredListHeight ?? AvailableListHeight) + delta);
        UpdateListHeight();
    }

    private void ListHeight_Reset(object sender, DoubleTappedRoutedEventArgs e)
    {
        preferredListHeight = null;
        UpdateListHeight();
        e.Handled = true;
    }
}
