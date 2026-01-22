using Avalonia.Data.Converters;
using Material.Icons;
using System;
using System.Globalization;

namespace CapyCard.Converters
{
    public class BoolToChevronConverter : IValueConverter
    {
        public static readonly BoolToChevronConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && isExpanded)
            {
                return MaterialIconKind.ChevronDown;
            }
            return MaterialIconKind.ChevronRight;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
