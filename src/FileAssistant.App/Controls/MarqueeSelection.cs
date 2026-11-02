using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;

namespace FileAssistant.App.Controls;

/// <summary>Blank-space drag selection for ListView and GridView, including virtualized/scrolled items.</summary>
public sealed class MarqueeSelection : Grid
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(ListViewBase), typeof(MarqueeSelection), new PropertyMetadata(null, TargetChanged));
    public ListViewBase? Target { get => (ListViewBase?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public static readonly DependencyProperty IsSelectingProperty = DependencyProperty.RegisterAttached(
        "IsSelecting", typeof(bool), typeof(MarqueeSelection), new PropertyMetadata(false));
    public static bool GetIsSelecting(DependencyObject owner) => (bool)owner.GetValue(IsSelectingProperty);
    public static readonly DependencyProperty IsExcludedProperty = DependencyProperty.RegisterAttached(
        "IsExcluded", typeof(bool), typeof(MarqueeSelection), new PropertyMetadata(false));
    public static bool GetIsExcluded(DependencyObject owner) => (bool)owner.GetValue(IsExcludedProperty);
    public static void SetIsExcluded(DependencyObject owner, bool value) => owner.SetValue(IsExcludedProperty, value);
    public event EventHandler? SelectionCommitted;

    private readonly Canvas overlay = new() { IsHitTestVisible = false };
    private readonly Border outline = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Dictionary<int, Rect> itemBounds = [];
    private HashSet<int> originalSelection = [];
    private Pointer? capturedPointer;
    private Panel? itemsPanel;
    private ScrollViewer? scroller;
    private Rect? lastRegion;
    private long lastFrame;
    private bool boundsDirty;
    private Point origin, pointerPosition;
    private bool dragging, additive;

    public MarqueeSelection()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        overlay.Children.Add(outline);
        Loaded += (_, _) =>
        {
            if (!Children.Contains(overlay)) Children.Add(overlay);
            SetRowSpan(overlay, Math.Max(1, RowDefinitions.Count));
            outline.Style = (Style)Application.Current.Resources["WorkspaceMarqueeStyle"];
            UpdateClip();
        };
        Unloaded += (_, _) => Finish(restore: false);
        SizeChanged += (_, _) => UpdateClip();
        AddHandler(PointerPressedEvent, new PointerEventHandler(Pressed), handledEventsToo: true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(Moved), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(Released), handledEventsToo: true);
        PointerCanceled += (_, _) => Finish(restore: true);
        PointerCaptureLost += (_, _) => Finish(restore: true);
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        { if (e.Key == VirtualKey.Escape && capturedPointer is not null) { Finish(restore: true); e.Handled = true; } }), handledEventsToo: true);
    }

    private static void TargetChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var surface = (MarqueeSelection)owner;
        surface.Finish(restore: false);
        if (e.OldValue is ListViewBase old) { old.SetValue(IsSelectingProperty, false); old.Items.VectorChanged -= surface.ItemsChanged; }
        if (e.NewValue is ListViewBase next) next.Items.VectorChanged += surface.ItemsChanged;
    }

    private void ItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs e) => Finish(restore: false);
    private void UpdateClip()
    { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, ActualHeight) }; boundsDirty = true; }
    private void Scrolled(object? sender, ScrollViewerViewChangedEventArgs e) => boundsDirty = true;
    private void PanelSizeChanged(object sender, SizeChangedEventArgs e) => boundsDirty = true;

    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (capturedPointer is not null || Target is not { IsEnabled: true, Visibility: Visibility.Visible } list ||
            list.Items.Count == 0 || list.SelectionMode is not (ListViewSelectionMode.Multiple or ListViewSelectionMode.Extended) ||
            e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var source = e.OriginalSource as DependencyObject; source is not null && source != this; source = VisualTreeHelper.GetParent(source))
            if (GetIsExcluded(source) || source is SelectorItem or ButtonBase or ScrollBar or Thumb or TextBox or RichEditBox) return;
        itemsPanel = list.ItemsPanelRoot;
        if (itemsPanel is null) return;
        pointerPosition = e.GetCurrentPoint(this).Position;
        originalSelection = SelectedIndices(list);
        additive = (e.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)) != 0;
        origin = TransformToVisual(itemsPanel).TransformPoint(pointerPosition);
        itemBounds.Clear();
        scroller = FindScroller(list);
        if (!CapturePointer(e.Pointer)) return;
        capturedPointer = e.Pointer;
        boundsDirty = true; lastRegion = null;
        itemsPanel.SizeChanged += PanelSizeChanged;
        if (scroller is not null) scroller.ViewChanged += Scrolled;
        e.Handled = true;
    }

    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (capturedPointer?.PointerId != e.Pointer.PointerId || itemsPanel is null) return;
        pointerPosition = e.GetCurrentPoint(this).Position;
        if (!dragging)
        {
            var current = TransformToVisual(itemsPanel).TransformPoint(pointerPosition);
            if (Math.Abs(current.X - origin.X) < 4 && Math.Abs(current.Y - origin.Y) < 4) return;
            dragging = true;
            Target!.SetValue(IsSelectingProperty, true);
            Target!.Focus(FocusState.Pointer);
            lastFrame = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += Render;
        }
        e.Handled = true;
    }

    private void Render(object? sender, object e)
    {
        if (!dragging) return;
        var frame = Stopwatch.GetTimestamp();
        var elapsed = Math.Clamp(Stopwatch.GetElapsedTime(lastFrame, frame).TotalMilliseconds, 1, 50);
        lastFrame = frame;
        AutoScroll(elapsed / 32);
        UpdateSelection();
    }

    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (capturedPointer?.PointerId != e.Pointer.PointerId) return;
        pointerPosition = e.GetCurrentPoint(this).Position;
        if (dragging) UpdateSelection();
        else if (!additive) SetSelection([]);
        Finish(restore: false);
        e.Handled = true;
    }

    private void AutoScroll(double elapsedScale)
    {
        if (scroller is null || scroller.ScrollableHeight <= 0) return;
        var point = TransformToVisual(scroller).TransformPoint(pointerPosition);
        var speed = point.Y < 28 ? -Math.Min(32, 28 - point.Y) : point.Y > scroller.ActualHeight - 28 ? Math.Min(32, point.Y - scroller.ActualHeight + 28) : 0;
        if (speed != 0) scroller.ChangeView(null, Math.Clamp(scroller.VerticalOffset + speed * elapsedScale, 0, scroller.ScrollableHeight), null, disableAnimation: true);
    }

    private void UpdateSelection()
    {
        if (itemsPanel is null || Target is not { } list) return;
        var point = TransformToVisual(itemsPanel).TransformPoint(pointerPosition);
        var region = new Rect(Math.Min(origin.X, point.X), Math.Min(origin.Y, point.Y), Math.Abs(point.X - origin.X), Math.Abs(point.Y - origin.Y));
        var refreshBounds = boundsDirty;
        if (refreshBounds)
        {
            // Item positions are stable while only selection changes. Re-read realized
            // containers after scrolling/resize, retaining visited off-screen rows.
            foreach (var item in itemsPanel.Children.OfType<SelectorItem>())
            {
                var index = list.IndexFromContainer(item);
                if (index >= 0) itemBounds[index] = item.TransformToVisual(itemsPanel).TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
            }
            boundsDirty = false;
        }
        if (!refreshBounds && lastRegion == region) return;
        lastRegion = region;
        var selected = additive ? new HashSet<int>(originalSelection) : [];
        foreach (var (index, bounds) in itemBounds)
            if (bounds.Right > region.Left && bounds.Left < region.Right && bounds.Bottom > region.Top && bounds.Top < region.Bottom) selected.Add(index);
        SetSelection(selected);
        var visible = itemsPanel.TransformToVisual(this).TransformBounds(region);
        Canvas.SetLeft(outline, visible.X); Canvas.SetTop(outline, visible.Y);
        outline.Width = visible.Width; outline.Height = visible.Height;
        outline.Visibility = Visibility.Visible;
    }

    private static HashSet<int> SelectedIndices(ListViewBase list) => list.SelectedRanges
        .SelectMany(range => Enumerable.Range(range.FirstIndex, checked((int)range.Length))).ToHashSet();

    private void SetSelection(HashSet<int> desired)
    {
        if (Target is not { } list) return;
        var selected = SelectedIndices(list);
        foreach (var range in Ranges(selected.Except(desired))) list.DeselectRange(range);
        foreach (var range in Ranges(desired.Except(selected))) list.SelectRange(range);
    }

    private static IEnumerable<ItemIndexRange> Ranges(IEnumerable<int> indices)
    {
        var sorted = indices.Order().ToArray();
        for (var i = 0; i < sorted.Length; i++)
        {
            var start = sorted[i];
            while (i + 1 < sorted.Length && sorted[i + 1] == sorted[i] + 1) i++;
            yield return new ItemIndexRange(start, (uint)(sorted[i] - start + 1));
        }
    }

    private void Finish(bool restore)
    {
        var pointer = capturedPointer;
        var wasDragging = dragging;
        capturedPointer = null;
        CompositionTarget.Rendering -= Render;
        outline.Visibility = Visibility.Collapsed;
        if (restore && pointer is not null) SetSelection(originalSelection);
        if (Target is not null) Target.SetValue(IsSelectingProperty, false);
        if (itemsPanel is not null) itemsPanel.SizeChanged -= PanelSizeChanged;
        if (scroller is not null) scroller.ViewChanged -= Scrolled;
        dragging = false; itemsPanel = null; scroller = null; itemBounds.Clear();
        if (pointer is not null) ReleasePointerCapture(pointer);
        // Native highlights stay live; previews/plans receive one final selection.
        if (wasDragging) SelectionCommitted?.Invoke(this, EventArgs.Empty);
    }

    private static ScrollViewer? FindScroller(DependencyObject owner)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(owner); index++)
        {
            var child = VisualTreeHelper.GetChild(owner, index);
            if (child is ScrollViewer found) return found;
            if (FindScroller(child) is { } nested) return nested;
        }
        return null;
    }
}
