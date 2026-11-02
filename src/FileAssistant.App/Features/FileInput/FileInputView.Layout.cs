using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace FileAssistant.App.Features.FileInput;

public sealed partial class FileInputView
{
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
