using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FileAssistant.App.Controls;

public sealed class KeyboardFocusOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is FocusState.Keyboard ? 1d : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
