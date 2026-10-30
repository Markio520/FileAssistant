using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FileAssistant.App.Features.Search;

public sealed class ColumnWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => new GridLength((double)value);
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
