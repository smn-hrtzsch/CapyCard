using Avalonia.Data.Converters;
using Material.Icons;
using System;
using System.Globalization;

namespace CapyCard.Converters
{
    public class GridViewToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isGridView)
            {
                return isGridView ? MaterialIconKind.ViewList : MaterialIconKind.ViewGrid;
            }
            return MaterialIconKind.ViewGrid;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class GridViewToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isGridView)
            {
                return isGridView ? "Liste" : "Raster";
            }
            return "Raster";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
