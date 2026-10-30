using System;
using System.Runtime.InteropServices;
using FileAssistant.App.Features.Workspace;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace FileAssistant.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        SetWorkspaceSize(compact: false);
        RootFrame.Navigate(typeof(WorkspacePage));
        UpdateCaptionColors();
    }

    public void SetWorkspaceSize(bool compact)
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96d;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)((compact ? 960 : 1320) * scale), area.Width - 48);
        var height = Math.Min((int)((compact ? 640 : 820) * scale), area.Height - 48);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = Math.Min((int)(940 * scale), area.Width);
            presenter.PreferredMinimumHeight = Math.Min((int)(600 * scale), area.Height);
        }

        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
    }

    private void WindowRoot_ActualThemeChanged(FrameworkElement sender, object args) => UpdateCaptionColors();

    private void UpdateCaptionColors()
    {
        if (WindowRoot is null) return;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = WindowRoot.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = WindowRoot.ActualTheme == ElementTheme.Dark ? Colors.LightGray : Colors.DimGray;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
