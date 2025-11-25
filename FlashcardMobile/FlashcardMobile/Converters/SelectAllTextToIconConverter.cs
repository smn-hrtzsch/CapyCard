using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FlashcardMobile.Converters
{
    public class SelectAllTextToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // "Alle abwählen" implies we want to deselect everything.
                // The user requested: "für das abwählen das Grid svg ohne abgehakte Boxen" (grid empty).
                // This button action will be "Deselect All".
                if (text.Contains("abwählen", StringComparison.OrdinalIgnoreCase)) 
                {
                     // "Alle abwählen" -> State is: All Selected.
                     // User wants to see the Checked Box here.
                     // Checked Box (Thin, Rounded)
                     return StreamGeometry.Parse("M5 3C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3H5ZM5 5H19V19H5V5ZM10 15L7 12L8.4 10.6L10 12.2L15.6 6.6L17 8L10 15Z");
                }
                else 
                {
                     // "Alle auswählen" -> State is: None/Mixed Selected.
                     // User wants to see the Empty Box here.
                     // Empty Box (Thin, Rounded)
                     return StreamGeometry.Parse("M5 3C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3H5ZM5 5H19V19H5V5Z");
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
