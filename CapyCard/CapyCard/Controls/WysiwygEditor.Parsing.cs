using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CapyCard.Controls
{
    /// <summary>
    /// WysiwygEditor - Markdown-Parsing und formatierte Anzeige
    /// </summary>
    public partial class WysiwygEditor
    {
        #region Display Update

        /// <summary>
        /// Aktualisiert die formatierte Anzeige im TextBlock.
        /// </summary>
        private void UpdateFormattedDisplay()
        {
            var editorText = EditorTextBox.Text ?? string.Empty;
            
            // Watermark-Sichtbarkeit aktualisieren
            UpdateWatermarkVisibility();
            
            // Inlines initialisieren falls nötig
            if (FormattedDisplay.Inlines == null)
            {
                FormattedDisplay.Inlines = new InlineCollection();
            }
            
            // Inlines leeren
            FormattedDisplay.Inlines.Clear();
            
            // Wenn leer, nichts anzeigen
            if (string.IsNullOrEmpty(editorText))
            {
                return;
            }
            
            // Platzhalter durch echte Base64-Daten ersetzen für die Anzeige
            var displayMarkdown = ConvertPlaceholdersToBase64(editorText);
            
            // Formatierte Inlines erstellen und hinzufügen
            var inlines = ParseMarkdownToInlines(displayMarkdown);
            foreach (var inline in inlines)
            {
                FormattedDisplay.Inlines.Add(inline);
            }
        }

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// Parst Markdown-Text und gibt formatierte Inlines zurück.
        /// </summary>
        public static InlineCollection ParseMarkdownToInlines(string markdown)
        {
            var inlines = new InlineCollection();
            
            if (string.IsNullOrEmpty(markdown))
            {
                return inlines;
            }

            // Zeilenweise parsen für Listen-Unterstützung
            var lines = markdown.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var processedLine = ProcessLineWithLists(line);
                
                // Füge die Inlines der Zeile hinzu
                foreach (var inline in processedLine)
                {
                    inlines.Add(inline);
                }
                
                // Zeilenumbruch hinzufügen (außer bei letzter Zeile)
                if (i < lines.Length - 1)
                {
                    inlines.Add(new LineBreak());
                }
            }

            return inlines;
        }

        /// <summary>
        /// Verarbeitet eine einzelne Zeile und erkennt Listen.
        /// </summary>
        private static List<Inline> ProcessLineWithLists(string line)
        {
            var result = new List<Inline>();
            
            // Prüfe auf ungeordnete Liste: "  - Text" oder "- Text"
            var unorderedMatch = Regex.Match(line, @"^(\s*)- (.*)$");
            if (unorderedMatch.Success)
            {
                var indent = unorderedMatch.Groups[1].Value;
                var content = unorderedMatch.Groups[2].Value;
                
                // Einrückung berechnen (2 Leerzeichen = 1 Ebene)
                var indentLevel = indent.Replace("\t", "  ").Length / 2;
                var bulletIndent = new string(' ', indentLevel * 4);
                
                // Aufzählungszeichen hinzufügen
                result.Add(new Run(bulletIndent + "• "));
                
                // Inhalt mit Formatierung parsen
                AddFormattedSegments(result, content);
                
                return result;
            }
            
            // Prüfe auf geordnete Liste: "  1. Text" oder "1. Text"
            var orderedMatch = Regex.Match(line, @"^(\s*)(\d+)\. (.*)$");
            if (orderedMatch.Success)
            {
                var indent = orderedMatch.Groups[1].Value;
                var number = orderedMatch.Groups[2].Value;
                var content = orderedMatch.Groups[3].Value;
                
                // Einrückung berechnen
                var indentLevel = indent.Replace("\t", "  ").Length / 2;
                var numberIndent = new string(' ', indentLevel * 4);
                
                // Nummer hinzufügen
                result.Add(new Run(numberIndent + number + ". "));
                
                // Inhalt mit Formatierung parsen
                AddFormattedSegments(result, content);
                
                return result;
            }
            
            // Normale Zeile ohne Liste
            AddFormattedSegments(result, line);

            return result;
        }

        /// <summary>
        /// Fügt formatierte Segmente zur Liste hinzu.
        /// </summary>
        private static void AddFormattedSegments(List<Inline> result, string text)
        {
            var segments = ParseToSegments(text);
            foreach (var segment in segments)
            {
                if (segment.IsImage && !string.IsNullOrEmpty(segment.ImagePath))
                {
                    // Bild als InlineUIContainer hinzufügen
                    try
                    {
                        var image = new Image
                        {
                            MaxWidth = 300,
                            MaxHeight = 200,
                            Stretch = Stretch.Uniform,
                            Margin = new Avalonia.Thickness(2)
                        };
                        
                        // Prüfe ob Base64-Data-URI oder Dateipfad
                        if (segment.ImagePath.StartsWith("data:"))
                        {
                            // Base64-Data-URI parsen
                            var bitmap = LoadImageFromDataUri(segment.ImagePath);
                            if (bitmap != null)
                            {
                                image.Source = bitmap;
                                result.Add(new InlineUIContainer(image));
                            }
                            else
                            {
                                result.Add(new Run("[Bild konnte nicht geladen werden]") 
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
                            result.Add(new InlineUIContainer(image));
                        }
                        else
                        {
                            // Fallback: Zeige Platzhalter-Text
                            result.Add(new Run($"[Bild nicht gefunden]") 
                            { 
                                Foreground = Brushes.Gray,
                                FontStyle = FontStyle.Italic
                            });
                        }
                    }
                    catch
                    {
                        // Bei Fehler: Zeige Platzhalter
                        result.Add(new Run($"[Bild nicht gefunden: {segment.ImagePath}]") 
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
                        run.TextDecorations = TextDecorations.Underline;
                    if (segment.IsHighlight)
                    {
                        // Orange-Gelb mit guter Lesbarkeit in Dark und Light Mode
                        run.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber
                        run.Foreground = Brushes.Black; // Schwarze Schrift für maximalen Kontrast
                    }
                    
                    result.Add(run);
                }
            }
        }

        /// <summary>
        /// Lädt ein Bild aus einer Base64-Data-URI.
        /// </summary>
        private static Bitmap? LoadImageFromDataUri(string dataUri)
        {
            try
            {
                // Format: data:image/png;base64,xxxxx
                var match = Regex.Match(dataUri, @"^data:image/[^;]+;base64,(.+)$");
                if (!match.Success) return null;
                
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

        /// <summary>
        /// Entfernt Markdown-Formatierung und gibt reinen Text zurück.
        /// </summary>
        public static string StripMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var result = markdown;
            
            // Entferne Bilder: ![alt](url)
            result = Regex.Replace(result, @"!\[.*?\]\(.*?\)", "");

            // Entferne Bold
            result = BoldRegex.Replace(result, "$1");
            // Entferne Underline (vor Italic, da __ vor * kommt)
            result = UnderlineRegex.Replace(result, "$1");
            // Entferne Italic
            result = ItalicRegex.Replace(result, "$1");
            // Entferne Highlight
            result = Regex.Replace(result, "==(.+?)==", "$1");

            return result.Trim();
        }

        /// <summary>
        /// Parst Markdown in Segmente mit Formatierungsinformationen.
        /// </summary>
        private static List<TextSegment> ParseToSegments(string text)
        {
            var segments = new List<TextSegment>();
            var currentIndex = 0;

            // Kombiniertes Pattern für alle Formatierungen (inkl. Highlight und Bilder)
            // Reihenfolge wichtig: Längere Patterns zuerst
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
                        Text = match.Groups[2].Value, // Alt-Text
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

        /// <summary>
        /// Internes Segment für Text mit Formatierung.
        /// </summary>
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

        #endregion
    }
}
