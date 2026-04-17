using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ImageViewerUno.Converters;

public class NullImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
