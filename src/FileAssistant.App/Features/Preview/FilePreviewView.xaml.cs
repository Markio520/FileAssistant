using System.Runtime.InteropServices.WindowsRuntime;
using FileAssistant.Core.Engines;
using FileAssistant.Core.Features.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace FileAssistant.App.Features.Preview;

public sealed partial class FilePreviewView : UserControl
{
    private CancellationTokenSource? loading;
    public static readonly DependencyProperty FileProperty = DependencyProperty.Register(nameof(File), typeof(SearchFile),
        typeof(FilePreviewView), new PropertyMetadata(null, (owner, _) => ((FilePreviewView)owner).Refresh()));
    public SearchFile? File { get => (SearchFile?)GetValue(FileProperty); set => SetValue(FileProperty, value); }
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string),
        typeof(FilePreviewView), new PropertyMetadata("\uE8A5", (owner, e) => ((FilePreviewView)owner).FallbackIcon.Glyph = (string)e.NewValue));
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public FilePreviewView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => { loading?.Cancel(); PreviewImage.Source = null; };
    }

    private async void Refresh()
    {
        loading?.Cancel();
        loading = null;
        if (!IsLoaded) return;
        PreviewImage.Source = null; TextPreview.Text = string.Empty;
        TextViewport.Visibility = StatusPanel.Visibility = Visibility.Collapsed;
        FallbackIcon.Visibility = Visibility.Visible;
        if (File is null) { PreviewLoading.IsActive = false; PreviewLoading.Visibility = Visibility.Collapsed; return; }
        using var cancellation = new CancellationTokenSource();
        loading = cancellation;
        PreviewLoading.IsActive = true; PreviewLoading.Visibility = Visibility.Visible;
        try
        {
            var result = await App.FileProcessing.PreviewAsync(File.FullPath, 700, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (result.Png is { } png)
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(png.AsBuffer()); stream.Seek(0);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                cancellation.Token.ThrowIfCancellationRequested();
                PreviewImage.Source = bitmap; FallbackIcon.Visibility = Visibility.Collapsed;
            }
            else if (result.Kind == PreviewKind.Text)
            { TextPreview.Text = result.Text; TextViewport.Visibility = Visibility.Visible; FallbackIcon.Visibility = Visibility.Collapsed; }
            if (!string.IsNullOrEmpty(result.Message)) ShowStatus(result.Message);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is FileProcessingException or IOException)
        { if (!cancellation.IsCancellationRequested) ShowStatus(error.Message); }
        catch (Exception)
        { if (!cancellation.IsCancellationRequested) ShowStatus("暂时无法显示此预览，可打开原文件查看。"); }
        finally
        {
            if (ReferenceEquals(loading, cancellation))
            { loading = null; PreviewLoading.IsActive = false; PreviewLoading.Visibility = Visibility.Collapsed; }
        }
    }
    private void ShowStatus(string message) { StatusText.Text = message; StatusPanel.Visibility = Visibility.Visible; }
}
