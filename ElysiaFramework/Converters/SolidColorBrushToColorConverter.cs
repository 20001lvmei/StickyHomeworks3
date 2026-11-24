using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ElysiaFramework.Converters;

public class SolidColorBrushToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var v = (SolidColorBrush)value;
        return v.Color;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 单向转换，回写时不做任何更新
        return Binding.DoNothing;
    }
}