using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CapyCard.Converters
{
    public class CardHasImagesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // Check for images: ![label](source)
                return Regex.IsMatch(text, @"!\[.*?\]\(.*?\)");
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
