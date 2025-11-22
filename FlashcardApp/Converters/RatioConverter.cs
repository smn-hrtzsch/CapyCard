using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FlashcardApp.Converters
{
    public class RatioConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d && parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out double ratio))
            {
                return d * ratio;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
