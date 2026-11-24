using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StickyHomeworks.Converter;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return new SolidColorBrush((Color)value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 单向转换，回写时不做任何更新
        return Binding.DoNothing;
    }
}