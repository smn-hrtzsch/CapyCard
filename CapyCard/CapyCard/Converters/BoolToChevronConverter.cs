using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace CapyCard.Converters
{
    public class BoolToChevronConverter : IValueConverter
    {
        public static readonly BoolToChevronConverter Instance = new();

        // Chevron Down
        private static readonly Geometry ChevronDown = Geometry.Parse("M 7.41 8.59 L 12 13.17 L 16.59 8.59 L 18 10 L 12 16 L 6 10 L 7.41 8.59 Z");
        // Chevron Right
        private static readonly Geometry ChevronRight = Geometry.Parse("M 8.59 16.59 L 13.17 12 L 8.59 7.41 L 10 6 L 16 12 L 10 18 L 8.59 16.59 Z");

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && isExpanded)
            {
                return ChevronDown;
            }
            return ChevronRight;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
