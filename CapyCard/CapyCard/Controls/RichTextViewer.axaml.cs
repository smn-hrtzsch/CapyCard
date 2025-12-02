using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CapyCard.Services;
using System.IO;

namespace CapyCard.Controls
{
    /// <summary>
    /// Zeigt Markdown-formatierten Text an.
    /// Unterstützt: Fett, Kursiv, Unterstrichen, Listen, Bilder.
    /// </summary>
    public partial class RichTextViewer : UserControl
    {
        // Dependency Properties
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<RichTextViewer, string>(nameof(Markdown), string.Empty);

        public static readonly new StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<RichTextViewer, double>(nameof(FontSize), 14);

        public static readonly StyledProperty<double> MaxImageHeightProperty =
            AvaloniaProperty.Register<RichTextViewer, double>(nameof(MaxImageHeight), 200);

        /// <summary>
        /// Der anzuzeigende Markdown-Text.
        /// </summary>
        public string Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        /// <summary>
        /// Schriftgröße für den Text.
        /// </summary>
        public new double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>
        /// Maximale Höhe für Bilder.
        /// </summary>
        public double MaxImageHeight
        {
            get => GetValue(MaxImageHeightProperty);
            set => SetValue(MaxImageHeightProperty, value);
        }

        // Regex patterns
        private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex UnderlinePattern = new(@"__(.+?)__", RegexOptions.Compiled);
        private static readonly Regex HighlightPattern = new(@"==(.+?)==", RegexOptions.Compiled);
        private static readonly Regex ImagePattern = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex BulletPattern = new(@"^[•\-\*]\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex NumberPattern = new(@"^(\d+)\.\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

        public RichTextViewer()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MarkdownProperty || 
                change.Property == FontSizeProperty ||
                change.Property == MaxImageHeightProperty)
            {
                RenderMarkdown();
            }
        }

        private void RenderMarkdown()
        {
            ContentContainer.ItemsSource = null;
            
            var markdown = Markdown;
            if (string.IsNullOrEmpty(markdown))
            {
                return;
            }

            var elements = new List<Control>();
            var lines = markdown.Split('\n');
            
            foreach (var line in lines)
            {
                var trimmedLine = line.TrimStart();
                
                // Prüfe auf Bild
                var imageMatch = ImagePattern.Match(trimmedLine);
                if (imageMatch.Success)
                {
                    var altText = imageMatch.Groups[1].Value;
                    var imageId = imageMatch.Groups[2].Value;
                    var imageControl = CreateImageControl(imageId, altText);
                    if (imageControl != null)
                    {
                        elements.Add(imageControl);
                    }
                    else
                    {
                        // Fallback: Zeige Platzhalter
                        elements.Add(CreatePlaceholderImage(altText));
                    }
                    continue;
                }

                // Prüfe auf Bullet-Liste
                var bulletMatch = BulletPattern.Match(trimmedLine);
                if (bulletMatch.Success)
                {
                    var content = bulletMatch.Groups[1].Value;
                    elements.Add(CreateBulletItem(content));
                    continue;
                }

                // Prüfe auf nummerierte Liste
                var numberMatch = NumberPattern.Match(trimmedLine);
                if (numberMatch.Success)
                {
                    var number = numberMatch.Groups[1].Value;
                    var content = numberMatch.Groups[2].Value;
                    elements.Add(CreateNumberedItem(number, content));
                    continue;
                }

                // Normaler Text mit Formatierung
                if (!string.IsNullOrWhiteSpace(line))
                {
                    elements.Add(CreateFormattedTextBlock(line));
                }
            }

            ContentContainer.ItemsSource = elements;
        }

        private TextBlock CreateFormattedTextBlock(string text)
        {
            var textBlock = new TextBlock
            {
                FontSize = FontSize,
                TextWrapping = TextWrapping.Wrap
            };

            ParseAndAddInlines(textBlock.Inlines!, text);
            return textBlock;
        }

        private void ParseAndAddInlines(InlineCollection inlines, string text)
        {
            var segments = ParseFormattedText(text);
            
            foreach (var segment in segments)
            {
                var run = new Run(segment.Text);
                
                if (segment.IsBold)
                    run.FontWeight = FontWeight.Bold;
                if (segment.IsItalic)
                    run.FontStyle = FontStyle.Italic;
                if (segment.IsUnderline)
                    run.TextDecorations = TextDecorations.Underline;
                if (segment.IsHighlight)
                    run.Background = new SolidColorBrush(Color.FromRgb(255, 235, 59)); // Yellow highlight
                
                inlines.Add(run);
            }
        }

        private List<TextSegment> ParseFormattedText(string text)
        {
            var segments = new List<TextSegment>();
            var currentIndex = 0;

            // Vereinfachtes Parsing - behandle Formatierungen sequentiell
            while (currentIndex < text.Length)
            {
                // Suche nach der nächsten Formatierung
                var boldMatch = BoldPattern.Match(text, currentIndex);
                var underlineMatch = UnderlinePattern.Match(text, currentIndex);
                var highlightMatch = HighlightPattern.Match(text, currentIndex);
                var italicMatch = ItalicPattern.Match(text, currentIndex);

                // Finde die nächste Übereinstimmung
                Match? nextMatch = null;
                string matchType = "";

                if (boldMatch.Success && (nextMatch == null || boldMatch.Index < nextMatch.Index))
                {
                    nextMatch = boldMatch;
                    matchType = "bold";
                }
                if (underlineMatch.Success && (nextMatch == null || underlineMatch.Index < nextMatch.Index))
                {
                    nextMatch = underlineMatch;
                    matchType = "underline";
                }
                if (highlightMatch.Success && (nextMatch == null || highlightMatch.Index < nextMatch.Index))
                {
                    nextMatch = highlightMatch;
                    matchType = "highlight";
                }
                if (italicMatch.Success && (nextMatch == null || italicMatch.Index < nextMatch.Index))
                {
                    nextMatch = italicMatch;
                    matchType = "italic";
                }

                if (nextMatch != null && nextMatch.Index >= currentIndex)
                {
                    // Text vor der Formatierung
                    if (nextMatch.Index > currentIndex)
                    {
                        segments.Add(new TextSegment(text.Substring(currentIndex, nextMatch.Index - currentIndex)));
                    }

                    // Formatierter Text
                    var content = nextMatch.Groups[1].Value;
                    var segment = new TextSegment(content)
                    {
                        IsBold = matchType == "bold",
                        IsItalic = matchType == "italic",
                        IsUnderline = matchType == "underline",
                        IsHighlight = matchType == "highlight"
                    };
                    segments.Add(segment);

                    currentIndex = nextMatch.Index + nextMatch.Length;
                }
                else
                {
                    // Restlicher Text
                    segments.Add(new TextSegment(text.Substring(currentIndex)));
                    break;
                }
            }

            if (segments.Count == 0)
            {
                segments.Add(new TextSegment(text));
            }

            return segments;
        }

        private StackPanel CreateBulletItem(string content)
        {
            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };

            panel.Children.Add(new TextBlock
            {
                Text = "•",
                FontSize = FontSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            });

            var textBlock = CreateFormattedTextBlock(content);
            panel.Children.Add(textBlock);

            return panel;
        }

        private StackPanel CreateNumberedItem(string number, string content)
        {
            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"{number}.",
                FontSize = FontSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                MinWidth = 20
            });

            var textBlock = CreateFormattedTextBlock(content);
            panel.Children.Add(textBlock);

            return panel;
        }

        private Control? CreateImageControl(string imageId, string altText)
        {
            try
            {
                var cardImage = ImageService.GetCardImage(imageId);
                if (cardImage == null) return null;

                var imageBytes = Convert.FromBase64String(cardImage.Base64Data);
                using var stream = new MemoryStream(imageBytes);
                var bitmap = new Bitmap(stream);

                return new Image
                {
                    Source = bitmap,
                    MaxHeight = MaxImageHeight,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };
            }
            catch
            {
                return null;
            }
        }

        private Border CreatePlaceholderImage(string altText)
        {
            return new Border
            {
                Background = Brushes.LightGray,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 8),
                Child = new TextBlock
                {
                    Text = string.IsNullOrEmpty(altText) ? "[Bild]" : $"[{altText}]",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyle.Italic
                }
            };
        }

        private class TextSegment
        {
            public string Text { get; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsUnderline { get; set; }
            public bool IsHighlight { get; set; }

            public TextSegment(string text)
            {
                Text = text;
            }
        }
    }
}
