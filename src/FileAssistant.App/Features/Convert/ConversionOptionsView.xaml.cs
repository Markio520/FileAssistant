using FileAssistant.App.Features.FileInput;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileAssistant.App.Features.Convert;

public sealed partial class ConversionOptionsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(ConversionViewModel),
        typeof(ConversionOptionsView), new PropertyMetadata(null, (owner, _) => ((ConversionOptionsView)owner).Bindings.Update()));
    public ConversionViewModel ViewModel { get => (ConversionViewModel)GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }
    public ConversionOptionsView() => InitializeComponent();
    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var paths = await FileInputSources.PickFolderAsync();
            if (paths.Count > 0) ViewModel.OutputDirectory = paths[0];
        }
        catch (Exception) { ViewModel.Feedback = "无法打开目录选择器，也可以直接填写输出文件夹。"; }
    }
}
