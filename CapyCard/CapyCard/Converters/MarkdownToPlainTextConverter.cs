using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CapyCard.Services;

namespace CapyCard.Converters
{
    /// <summary>
    /// Konvertiert Markdown-Text in Plain-Text für Vorschau-Anzeigen.
    /// Entfernt Formatierung und ersetzt Bilder durch "[Bild]".
    /// </summary>
    public class MarkdownToPlainTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string markdown && !string.IsNullOrEmpty(markdown))
            {
                return MarkdownService.StripMarkdown(markdown);
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("MarkdownToPlainTextConverter ist nur für Einweg-Konvertierung gedacht.");
        }
    }
}
