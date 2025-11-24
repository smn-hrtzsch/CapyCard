using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FlashcardMobile.Converters
{
    public class RatioConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double ratio = 0;
            if (parameter is double r)
            {
                ratio = r;
            }
            else if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                ratio = parsed;
            }

            if (value is double d && ratio > 0)
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
