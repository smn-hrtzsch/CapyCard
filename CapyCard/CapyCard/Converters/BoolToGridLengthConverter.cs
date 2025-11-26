using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace CapyCard.Converters
{
    public class BoolToGridLengthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramStr)
            {
                var parts = paramStr.Split('|');
                if (parts.Length == 2)
                {
                    var lengthStr = boolValue ? parts[0] : parts[1];
                    return GridLength.Parse(lengthStr);
                }
            }
            return GridLength.Auto;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
