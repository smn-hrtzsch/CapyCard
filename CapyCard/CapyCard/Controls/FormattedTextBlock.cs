using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Material.Icons;
using Material.Icons.Avalonia;

namespace CapyCard.Controls
{
    /// <summary>
    /// Zeigt Text mit Markdown-Formatierung an (Bold, Italic, Underline, Highlight, Listen, Bilder).
    /// Readonly-Komponente für die Anzeige in LearnView etc.
    /// </summary>
    public class FormattedTextBlock : TextBlock
    {
        private static readonly Regex UnorderedListRegex = new(@"^(\s*)- (.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new(@"^(\s*)(\d+)\. (.*)$", RegexOptions.Compiled);

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

            if (change.Property == FormattedTextProperty || change.Property == ShowImageHintProperty)
            {
                UpdateInlines();
            }
        }

        private void UpdateInlines()
        {
            Inlines?.Clear();
            
            var text = FormattedText;
            bool hasText = !string.IsNullOrEmpty(text);
            var sourceLines = hasText ? text.Split('\n') : Array.Empty<string>();
            
            bool willShowHint = ShowImageHint;
            int totalDesiredLines = sourceLines.Length + (willShowHint ? 1 : 0);
            bool isTruncated = false;
            int maxTextLines = sourceLines.Length;

            // Manual truncation logic for explicit line breaks
            if (MaxLines > 0 && totalDesiredLines > MaxLines)
            {
                isTruncated = true;
                maxTextLines = (int)MaxLines - (willShowHint ? 1 : 0);
                if (maxTextLines < 0)
                {
                    maxTextLines = 0;
                    willShowHint = false;
                }
            }

            for (int i = 0; i < maxTextLines; i++)
            {
                ProcessLine(sourceLines[i]);
                if (i < maxTextLines - 1 || willShowHint)
                {
                    Inlines?.Add(new LineBreak());
                }
            }

            if (willShowHint)
            {
                // Icon einbetten
                var icon = new MaterialIcon
                {
                    Kind = MaterialIconKind.ImageOutline,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 2, 3, 0) // Mehr Abstand nach oben, weniger nach rechts
                };
                
                var container = new InlineUIContainer(icon)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };
                
                Inlines?.Add(container);

                var run = new Run("Zum Darstellen in Vorschau öffnen")
                {
                    FontStyle = FontStyle.Italic,
                    FontSize = 14
                };
                Inlines?.Add(run);
            }

            if (isTruncated)
            {
                Inlines?.Add(new Run("..."));
            }
        }

        private void ProcessLine(string line)
        {
            // Prüfe auf ungeordnete Liste
            var unorderedMatch = UnorderedListRegex.Match(line);
            if (unorderedMatch.Success)
            {
                var indent = unorderedMatch.Groups[1].Value;
                var content = unorderedMatch.Groups[2].Value;
                
                // Einrückung berechnen
                var indentLevel = indent.Replace("\t", "  ").Length / 2;
                var bulletIndent = new string(' ', indentLevel * 4);
                
                Inlines?.Add(new Run(bulletIndent + "• "));
                AddFormattedContent(content);
                return;
            }
            
            // Prüfe auf geordnete Liste
            var orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                var indent = orderedMatch.Groups[1].Value;
                var number = orderedMatch.Groups[2].Value;
                var content = orderedMatch.Groups[3].Value;
                
                // Einrückung berechnen
                var indentLevel = indent.Replace("\t", "  ").Length / 2;
                var numberIndent = new string(' ', indentLevel * 4);
                
                Inlines?.Add(new Run(numberIndent + number + ". "));
                AddFormattedContent(content);
                return;
            }
            
            // Normale Zeile
            AddFormattedContent(line);
        }

        private void AddFormattedContent(string text)
        {
            var segments = ParseToSegments(text);
            foreach (var segment in segments)
            {
                if (segment.IsImage && !string.IsNullOrEmpty(segment.ImagePath))
                {
                    // Bild hinzufügen
                    try
                    {
                        var image = new Avalonia.Controls.Image
                        {
                            MaxWidth = 300,
                            MaxHeight = 200,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(2),
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };

                        // Interaktion hinzufügen
                        image.PointerPressed += (s, e) =>
                        {
                            if (ImageClickCommand != null && image.Source != null)
                            {
                                if (ImageClickCommand.CanExecute(image.Source))
                                {
                                    ImageClickCommand.Execute(image.Source);
                                }
                            }
                        };
                        
                        // Prüfe ob Base64-Data-URI oder Dateipfad
                        if (segment.ImagePath.StartsWith("data:"))
                        {
                            // Base64-Data-URI parsen
                            var bitmap = LoadImageFromDataUri(segment.ImagePath);
                            if (bitmap != null)
                            {
                                image.Source = bitmap;
                                Inlines?.Add(new InlineUIContainer(image));
                            }
                            else
                            {
                                Inlines?.Add(new Run("[Bild konnte nicht geladen werden]") 
                                { 
                                    Foreground = Brushes.Gray,
                                    FontStyle = FontStyle.Italic
                                });
                            }
                        }
                        else if (System.IO.File.Exists(segment.ImagePath))
                        {
                            // Lokale Datei laden
                            image.Source = new Bitmap(segment.ImagePath);
                            Inlines?.Add(new InlineUIContainer(image));
                        }
                        else
                        {
                            Inlines?.Add(new Run($"[Bild nicht gefunden]") 
                            { 
                                Foreground = Brushes.Gray,
                                FontStyle = FontStyle.Italic
                            });
                        }
                    }
                    catch
                    {
                        Inlines?.Add(new Run($"[Bild nicht gefunden]") 
                        { 
                            Foreground = Brushes.Gray,
                            FontStyle = FontStyle.Italic
                        });
                    }
                }
                else
                {
                    var run = new Run(segment.Text);
                    
                    if (segment.IsBold)
                        run.FontWeight = FontWeight.Bold;
                    if (segment.IsItalic)
                        run.FontStyle = FontStyle.Italic;
                    if (segment.IsUnderline)
                        run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
                    if (segment.IsHighlight)
                    {
                        // Orange-Gelb mit guter Lesbarkeit in Dark und Light Mode
                        run.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber
                        run.Foreground = Brushes.Black; // Schwarze Schrift für maximalen Kontrast
                    }
                    
                    Inlines?.Add(run);
                }
            }
        }

        private static List<TextSegment> ParseToSegments(string text)
        {
            var segments = new List<TextSegment>();
            var currentIndex = 0;

            // Kombiniertes Pattern für alle Formatierungen (inkl. Highlight und Bilder)
            var combinedPattern = new Regex(
                @"(!\[([^\]]*)\]\(([^)]+)\))|(\*\*(.+?)\*\*)|(__(.+?)__)|(==(.+?)==)|(\*(.+?)\*)", 
                RegexOptions.Compiled);

            foreach (Match match in combinedPattern.Matches(text))
            {
                // Text vor dem Match
                if (match.Index > currentIndex)
                {
                    var beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        segments.Add(new TextSegment { Text = beforeText });
                    }
                }

                // Bild ![alt](url)
                if (match.Groups[1].Success)
                {
                    segments.Add(new TextSegment 
                    { 
                        Text = match.Groups[2].Value,
                        IsImage = true,
                        ImagePath = match.Groups[3].Value
                    });
                }
                // Bold **text**
                else if (match.Groups[4].Success)
                {
                    segments.Add(new TextSegment 
                    { 
                        Text = match.Groups[5].Value, 
                        IsBold = true 
                    });
                }
                // Underline __text__
                else if (match.Groups[6].Success)
                {
                    segments.Add(new TextSegment 
                    { 
                        Text = match.Groups[7].Value, 
                        IsUnderline = true 
                    });
                }
                // Highlight ==text==
                else if (match.Groups[8].Success)
                {
                    segments.Add(new TextSegment 
                    { 
                        Text = match.Groups[9].Value, 
                        IsHighlight = true 
                    });
                }
                // Italic *text*
                else if (match.Groups[10].Success)
                {
                    segments.Add(new TextSegment 
                    { 
                        Text = match.Groups[11].Value, 
                        IsItalic = true 
                    });
                }

                currentIndex = match.Index + match.Length;
            }

            // Restlicher Text
            if (currentIndex < text.Length)
            {
                segments.Add(new TextSegment 
                { 
                    Text = text.Substring(currentIndex) 
                });
            }

            // Falls keine Segmente, den ganzen Text hinzufügen
            if (segments.Count == 0 && !string.IsNullOrEmpty(text))
            {
                segments.Add(new TextSegment { Text = text });
            }

            return segments;
        }

        private class TextSegment
        {
            public string Text { get; set; } = string.Empty;
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsUnderline { get; set; }
            public bool IsHighlight { get; set; }
            public bool IsImage { get; set; }
            public string? ImagePath { get; set; }
        }
        
        /// <summary>
        /// Lädt ein Bild aus einer Base64-Data-URI.
        /// </summary>
        private static Bitmap? LoadImageFromDataUri(string dataUri)
        {
            try
            {
                // Format: data:image/png;base64,iVBORw0KGgo...
                var match = Regex.Match(dataUri, @"^data:image/[^;]+;base64,(.+)$");
                if (!match.Success)
                    return null;
                    
                var base64Data = match.Groups[1].Value;
                var imageBytes = Convert.FromBase64String(base64Data);
                using var stream = new System.IO.MemoryStream(imageBytes);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
