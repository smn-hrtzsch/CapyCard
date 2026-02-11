using System;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace CapyCard.Controls
{
    /// <summary>
    /// Zeigt Text mit Markdown-Formatierung an.
    /// Readonly-Komponente für die Anzeige in LearnView etc.
    /// </summary>
    public class FormattedTextBlock : TextBlock
    {
        public static readonly StyledProperty<string> FormattedTextProperty =
            AvaloniaProperty.Register<FormattedTextBlock, string>(nameof(FormattedText), string.Empty);

        public static readonly StyledProperty<ICommand?> ImageClickCommandProperty =
            AvaloniaProperty.Register<FormattedTextBlock, ICommand?>(nameof(ImageClickCommand));

        public static readonly StyledProperty<bool> ShowImageHintProperty =
            AvaloniaProperty.Register<FormattedTextBlock, bool>(nameof(ShowImageHint), false);

        /// <summary>
        /// Der Markdown-formatierte Text.
        /// </summary>
        public string FormattedText
        {
            get => GetValue(FormattedTextProperty);
            set => SetValue(FormattedTextProperty, value);
        }

        /// <summary>
        /// Zeigt einen Hinweis an, dass Bilder in der Vorschau verfügbar sind.
        /// </summary>
        public bool ShowImageHint
        {
            get => GetValue(ShowImageHintProperty);
            set => SetValue(ShowImageHintProperty, value);
        }

        /// <summary>
        /// Command, der ausgeführt wird, wenn auf ein Bild geklickt wird.
        /// Parameter ist das Image.Source Objekt.
        /// </summary>
        public ICommand? ImageClickCommand
        {
            get => GetValue(ImageClickCommandProperty);
            set => SetValue(ImageClickCommandProperty, value);
        }

        public FormattedTextBlock()
        {
            TextWrapping = TextWrapping.Wrap;
            TextTrimming = TextTrimming.CharacterEllipsis;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FormattedTextProperty ||
                change.Property == ShowImageHintProperty ||
                change.Property == ForegroundProperty)
            {
                UpdateInlines();
            }
        }

        private void UpdateInlines()
        {
            Inlines?.Clear();

            var text = FormattedText ?? string.Empty;
            var sourceLines = string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('\n');

            var willShowHint = ShowImageHint;
            var isTruncated = false;
            var maxTextLines = sourceLines.Length;

            // Manual truncation logic for explicit line breaks.
            if (MaxLines > 0 && sourceLines.Length > 0)
            {
                var totalNeededLines = sourceLines.Length + (willShowHint ? 1 : 0);

                if (totalNeededLines > MaxLines)
                {
                    isTruncated = true;
                    maxTextLines = (int)MaxLines - (willShowHint ? 1 : 0);
                    if (maxTextLines < 0)
                    {
                        maxTextLines = 0;
                        willShowHint = false;
                    }
                }
            }

            var textToRender = maxTextLines > 0
                ? string.Join('\n', sourceLines.Take(maxTextLines))
                : string.Empty;

            if (isTruncated && !willShowHint && !string.IsNullOrEmpty(textToRender))
            {
                textToRender += "...";
            }

            if (!string.IsNullOrEmpty(textToRender))
            {
                var formulaColor = Foreground is SolidColorBrush solidColorBrush
                    ? solidColorBrush.Color
                    : (Color?)null;

                var parsedInlines = WysiwygEditor.ParseMarkdownToInlines(
                    textToRender,
                    ConfigurePreviewImage,
                    formulaColor);
                foreach (var inline in parsedInlines)
                {
                    Inlines?.Add(inline);
                }
            }

            if (willShowHint)
            {
                if (!string.IsNullOrEmpty(textToRender))
                {
                    Inlines?.Add(new LineBreak());
                }

                var icon = new MaterialIcon
                {
                    Kind = MaterialIconKind.ImageOutline,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 2, 3, 0)
                };

                var container = new InlineUIContainer(icon)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };

                Inlines?.Add(container);

                var run = new Run("Zum Darstellen in Vorschau öffnen" + (isTruncated ? "..." : string.Empty))
                {
                    FontStyle = FontStyle.Italic,
                    FontSize = 14
                };
                Inlines?.Add(run);
            }
        }

        private void ConfigurePreviewImage(Image image)
        {
            image.Cursor = new Cursor(StandardCursorType.Hand);
            image.PointerPressed += (_, _) =>
            {
                if (ImageClickCommand != null && image.Source != null && ImageClickCommand.CanExecute(image.Source))
                {
                    ImageClickCommand.Execute(image.Source);
                }
            };
        }
    }
}
