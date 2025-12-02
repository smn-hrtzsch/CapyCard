using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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

        /// <summary>
        /// Der Markdown-formatierte Text.
        /// </summary>
        public string FormattedText
        {
            get => GetValue(FormattedTextProperty);
            set => SetValue(FormattedTextProperty, value);
        }

        public FormattedTextBlock()
        {
            TextWrapping = TextWrapping.Wrap;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FormattedTextProperty)
            {
                UpdateInlines();
            }
        }

        private void UpdateInlines()
        {
            Inlines?.Clear();
            
            var text = FormattedText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Zeilenweise parsen für Listen-Unterstützung
            var lines = text.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                ProcessLine(line);
                
                // Zeilenumbruch hinzufügen (außer bei letzter Zeile)
                if (i < lines.Length - 1)
                {
                    Inlines?.Add(new LineBreak());
                }
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
                            Margin = new Thickness(2)
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
