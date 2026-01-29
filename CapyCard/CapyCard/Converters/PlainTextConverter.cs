using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using CapyCard.Controls;

namespace CapyCard.Converters
{
    /// <summary>
    /// Konvertiert Text mit Markdown-Formatierung in reinen Text für Vorschau-Anzeigen.
    /// </summary>
    public class PlainTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                // Remove ONLY images for the grid view, keeping other markdown
                return Regex.Replace(text, @"!\[.*?\]\(.*?\)", "").Trim();
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
