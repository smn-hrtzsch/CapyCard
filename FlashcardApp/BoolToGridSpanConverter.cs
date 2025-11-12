using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FlashcardApp
{
    // Diese Hilfsklasse wandelt 'true' in 2 und 'false' in 1 um.
    // Wir benutzen das, damit der "Speichern"-Button die volle Breite (2 Spalten) einnimmt,
    // wenn der "Abbrechen"-Button unsichtbar ist.
    public class BoolToGridSpanConverter : IValueConverter
    {
        public static readonly BoolToGridSpanConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                // Wenn 'IsEditing' false ist (BoolToGridSpanConverter Inverter),
                // soll der Span 2 sein.
                // Oh, Moment, das Binding ist an '!IsEditing'.
                // Also wenn 'true' (weil !IsEditing true ist) -> Span 2
                // wenn 'false' (weil !IsEditing false ist) -> Span 1
                return b ? 2 : 1;
            }
            return 1;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}