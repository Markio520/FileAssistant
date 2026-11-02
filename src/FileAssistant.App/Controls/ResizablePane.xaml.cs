using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Controls;

/// <summary>Keeps a right pane resizable and collapsible without losing its preferred width.</summary>
public sealed partial class ResizablePane : UserControl
{
    private const double MinimumPaneWidth = 240;
    private const double MaximumPaneWidth = 560;

    public static readonly DependencyProperty MainContentProperty = DependencyProperty.Register(
        nameof(MainContent), typeof(UIElement), typeof(ResizablePane), new PropertyMetadata(null));
    public static readonly DependencyProperty PaneContentProperty = DependencyProperty.Register(
        nameof(PaneContent), typeof(UIElement), typeof(ResizablePane), new PropertyMetadata(null));
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen), typeof(bool), typeof(ResizablePane), new PropertyMetadata(true, LayoutChanged));
    public static readonly DependencyProperty PaneWidthProperty = DependencyProperty.Register(
        nameof(PaneWidth), typeof(double), typeof(ResizablePane), new PropertyMetadata(304d, LayoutChanged));
    public static readonly DependencyProperty PaneSpacingProperty = DependencyProperty.Register(
        nameof(PaneSpacing), typeof(double), typeof(ResizablePane), new PropertyMetadata(8d, LayoutChanged));
    public static readonly DependencyProperty CollapsedPaneContentProperty = DependencyProperty.Register(
        nameof(CollapsedPaneContent), typeof(UIElement), typeof(ResizablePane), new PropertyMetadata(null, LayoutChanged));
    public static readonly DependencyProperty MinimumContentWidthProperty = DependencyProperty.Register(
        nameof(MinimumContentWidth), typeof(double), typeof(ResizablePane), new PropertyMetadata(320d, LayoutChanged));

    public UIElement? MainContent { get => (UIElement?)GetValue(MainContentProperty); set => SetValue(MainContentProperty, value); }
    public UIElement? PaneContent { get => (UIElement?)GetValue(PaneContentProperty); set => SetValue(PaneContentProperty, value); }
    public bool IsPaneOpen { get => (bool)GetValue(IsPaneOpenProperty); set => SetValue(IsPaneOpenProperty, value); }
    public double PaneWidth { get => (double)GetValue(PaneWidthProperty); set => SetValue(PaneWidthProperty, value); }
    public double PaneSpacing { get => (double)GetValue(PaneSpacingProperty); set => SetValue(PaneSpacingProperty, value); }
    public UIElement? CollapsedPaneContent { get => (UIElement?)GetValue(CollapsedPaneContentProperty); set => SetValue(CollapsedPaneContentProperty, value); }
    public double MinimumContentWidth { get => (double)GetValue(MinimumContentWidthProperty); set => SetValue(MinimumContentWidthProperty, value); }

    public ResizablePane()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayoutWidths();
        Loaded += (_, _) => UpdateLayoutWidths();
    }

    private static void LayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((ResizablePane)sender).UpdateLayoutWidths();

    private double HandleWidth => double.IsFinite(PaneSpacing) ? Math.Max(8, PaneSpacing) : 8;
    private double AvailablePaneWidth => Math.Clamp(ActualWidth - HandleWidth - (double.IsFinite(MinimumContentWidth) ? Math.Max(0, MinimumContentWidth) : 320), MinimumPaneWidth, MaximumPaneWidth);

    private void UpdateLayoutWidths()
    {
        if (PaneColumn is null) return;
        Handle.Visibility = PanePresenter.Visibility = IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        var hasCollapsedContent = !IsPaneOpen && CollapsedPaneContent is not null;
        CollapsedPresenter.Visibility = hasCollapsedContent ? Visibility.Visible : Visibility.Collapsed;
        HandleColumn.Width = new GridLength(IsPaneOpen || hasCollapsedContent ? HandleWidth : 0);
        PaneColumn.Width = new GridLength(IsPaneOpen
            ? Math.Clamp(double.IsFinite(PaneWidth) ? PaneWidth : 304, MinimumPaneWidth, AvailablePaneWidth) : hasCollapsedContent ? 48 : 0);
    }

    private void Pane_ResizeRequested(object? sender, double delta)
    {
        if (IsPaneOpen && double.IsFinite(delta))
            PaneWidth = Math.Clamp(PaneColumn.ActualWidth - delta, MinimumPaneWidth, AvailablePaneWidth);
    }
}
