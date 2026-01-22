using Avalonia.Data.Converters;
using Material.Icons;
using System;
using System.Globalization;

namespace CapyCard.Converters
{
    public class SelectAllTextToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // "Alle abwählen" -> User wants to deselect all. Icon should represent "Selected" state (to show what happens? or state?)
                // Usually: Button says "Select All" -> Icon is Empty Box. Button says "Deselect All" -> Icon is Checked Box.
                if (text.Contains("abwählen", StringComparison.OrdinalIgnoreCase)) 
                {
                     // State is All Selected. Button action is Deselect.
                     // Use CheckboxMultipleMarked
                     return MaterialIconKind.CheckboxMultipleMarked;
                }
                else 
                {
                     // State is None/Mixed. Button action is Select All.
                     // Use CheckboxMultipleBlankOutline
                     return MaterialIconKind.CheckboxMultipleBlankOutline;
                }
            }
            return MaterialIconKind.CheckboxMultipleBlankOutline;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
