using System.Runtime.InteropServices.WindowsRuntime;
using FileAssistant.Core.Features.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace FileAssistant.App.Features.Preview;

/// <summary>Loads only realized file tiles; the shared local preview engine bounds decoding and caches images.</summary>
public sealed partial class FileThumbnail : UserControl
{
    private CancellationTokenSource? loading;
    public static readonly DependencyProperty FileProperty = DependencyProperty.Register(nameof(File), typeof(SearchFile), typeof(FileThumbnail),
        new PropertyMetadata(null, (owner, _) => ((FileThumbnail)owner).Refresh()));
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(FileThumbnail),
        new PropertyMetadata("\uE8A5", (owner, args) => ((FileThumbnail)owner).FallbackIcon.Glyph = (string)args.NewValue));
    public SearchFile? File { get => (SearchFile?)GetValue(FileProperty); set => SetValue(FileProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public FileThumbnail()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => { loading?.Cancel(); ThumbnailImage.Source = null; };
    }
    private async void Refresh()
    {
        loading?.Cancel();
        loading = null;
        if (!IsLoaded) return;
        ThumbnailImage.Source = null;
        FallbackIcon.Visibility = Visibility.Visible;
        if (File is null || File.IsFolder) return;
        using var request = new CancellationTokenSource();
        loading = request;
        try
        {
            var preview = await App.FileProcessing.PreviewAsync(File.FullPath, 300, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (preview.Png is not { } png) return;
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            request.Token.ThrowIfCancellationRequested();
            ThumbnailImage.Source = bitmap;
            FallbackIcon.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* The file icon remains available when a thumbnail cannot be decoded. */ }
        finally { if (ReferenceEquals(loading, request)) loading = null; }
    }
}
