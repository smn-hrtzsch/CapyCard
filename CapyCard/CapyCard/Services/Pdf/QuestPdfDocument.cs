using CapyCard.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapyCard.Services.Pdf
{
    public class QuestPdfDocument : IDocument
    {
        private readonly List<Card> _cards;
        private readonly int _columnCount;
        private readonly float _fontScale;
        private readonly float _cellHeight;
        private readonly int _rowCount;
        private readonly float _maxImageHeight;

        // Regex zum Extrahieren von Base64-Bildern und Markdown-Formatierung
        private static readonly Regex ImageExtractPattern = new(@"!\[([^\]]*)\]\((data:image/[^;]+;base64,([^)]+))\)", RegexOptions.Compiled);
        private static readonly Regex ImageRemovePattern = new(@"!\[[^\]]*\]\([^)]+\)", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new(@"\*(.+?)\*", RegexOptions.Compiled);
        private static readonly Regex UnderlinePattern = new(@"__(.+?)__", RegexOptions.Compiled);
        private static readonly Regex HighlightPattern = new(@"==(.+?)==", RegexOptions.Compiled);
        
        // Regex für Listen-Erkennung
        private static readonly Regex BulletListPattern = new(@"^\s*[-•]\s+", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex NumberedListPattern = new(@"^\s*\d+\.\s+", RegexOptions.Multiline | RegexOptions.Compiled);

        public QuestPdfDocument(List<Card> cards, int columnCount)
        {
            _cards = cards;
            _columnCount = columnCount;

            // Basierend auf der Spaltenanzahl werden Zeilenanzahl, Schrifgröße und Zellenhöhe angepasst
            (_rowCount, _fontScale, _cellHeight, _maxImageHeight) = columnCount switch
            {
                1 => (4, 1.5f, 6.5f, 5.0f),   // 4 Zeilen, 150% Schrift, 6.5cm Höhe, 5cm max Bild
                2 => (5, 1.2f, 5.1f, 3.5f),   // 5 Zeilen, 120% Schrift, 5.1cm Höhe, 3.5cm max Bild
                3 => (6, 1.0f, 4.2f, 2.8f),   // 6 Zeilen, 100% Schrift, 4.2cm Höhe, 2.8cm max Bild
                4 => (6, 0.9f, 3.8f, 2.4f),   // 7 Zeilen, 90% Schrift, 3.7cm Höhe, 2.4cm max Bild
                5 => (7, 0.8f, 3.3f, 2.0f),   // 8 Zeilen, 80% Schrift, 3.3cm Höhe, 2cm max Bild
                _ => (6, 1.0f, 4.2f, 2.8f)    // Fallback auf 3 Spalten
            };
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            var cardsPerPage = _rowCount * _columnCount;
            var cardChunks = _cards.Chunk(cardsPerPage);

            foreach (var chunk in cardChunks)
            {
                var pageCards = chunk.ToList();
                
                // --- SEITE 1: VORDERSEITEN ---
                container.Page(page =>
                {
                    // "Arial" is often mapped to Droid Sans or Roboto on Android by SkiaSharp
                    page.DefaultTextStyle(x => x.FontFamily("Arial")); 
                    
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < _columnCount; i++)
                                columns.RelativeColumn();
                        });

                        foreach (var card in pageCards)
                        {
                            var cell = table.Cell().Element(CardCellStyle).AlignMiddle();
                            
                            // Horizontale Zentrierung nur wenn keine Liste
                            if (!ContainsList(card.Front))
                                cell = cell.AlignCenter();
                            
                            cell.ScaleToFit().Column(col => RenderCardContent(col, card.Front));
                        }
                        
                        // Leere Zellen auffüllen
                        int emptyCells = cardsPerPage - pageCards.Count;
                        for (int i = 0; i < emptyCells; i++)
                        {
                            table.Cell().Element(CardCellStyle);
                        }
                    });
                });

                // --- SEITE 2: RÜCKSEITEN ---
                var mirroredBacks = new List<Card?>();
                for (int i = 0; i < pageCards.Count; i += _columnCount)
                {
                    var rowCards = pageCards.Skip(i).Take(_columnCount).ToList();
                    var fullRow = new List<Card?>(rowCards);
                    while (fullRow.Count < _columnCount) fullRow.Add(null);
                    fullRow.Reverse();
                    mirroredBacks.AddRange(fullRow);
                }

                container.Page(page =>
                {
                    // "Arial" is often mapped to Droid Sans or Roboto on Android by SkiaSharp
                    page.DefaultTextStyle(x => x.FontFamily("Arial")); 
                    
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);

                    page.Content()
                        .PaddingTop(2, Unit.Millimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (int i = 0; i < _columnCount; i++)
                                    columns.RelativeColumn();
                            });

                            foreach (var card in mirroredBacks)
                            {
                                var cell = table.Cell().Element(CardCellStyle);
                                if (card != null)
                                {
                                    var alignedCell = cell.AlignMiddle();
                                    
                                    // Horizontale Zentrierung nur wenn keine Liste
                                    if (!ContainsList(card.Back))
                                        alignedCell = alignedCell.AlignCenter();
                                    
                                    alignedCell.ScaleToFit().Column(col => RenderCardContent(col, card.Back));
                                }
                            }
                            
                            int emptyCells = cardsPerPage - mirroredBacks.Count;
                            for (int i = 0; i < emptyCells; i++)
                            {
                                table.Cell().Element(CardCellStyle);
                            }
                        });
                });
            }
        }
        
        private IContainer CardCellStyle(IContainer container)
        {
            return container
                .Border(1)
                .Padding(5)
                .Height(_cellHeight, Unit.Centimetre);
        }

        /// <summary>
        /// Rendert den Karteninhalt mit Text und Bildern.
        /// </summary>
        private void RenderCardContent(ColumnDescriptor column, string? content)
        {
            if (string.IsNullOrEmpty(content))
                return;

            // Bilder aus dem Content extrahieren
            var images = ExtractImages(content);
            
            // Text ohne Bilder extrahieren
            var textContent = ImageRemovePattern.Replace(content, "").Trim();

            // Prüfen ob der Text Listen enthält
            bool containsList = ContainsList(textContent);

            // Text mit Formatierung rendern (wenn vorhanden)
            if (!string.IsNullOrWhiteSpace(textContent))
            {
                if (containsList)
                {
                    // Listen linksbündig rendern
                    RenderListContent(column, textContent);
                }
                else
                {
                    // Normaler Text zentriert
                    column.Item()
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(12 * _fontScale));
                            text.AlignCenter();
                            RenderFormattedText(text, textContent);
                        });
                }
            }

            // Bilder rendern
            foreach (var imageData in images)
            {
                try
                {
                    var imageBytes = Convert.FromBase64String(imageData);
                    column.Item()
                        .AlignCenter()
                        .Image(imageBytes, ImageScaling.FitArea);
                }
                catch
                {
                    // Bei fehlerhaften Base64-Daten ignorieren
                }
            }
        }

        /// <summary>
        /// Prüft, ob der Text Listen (Bullet oder nummeriert) enthält.
        /// </summary>
        private static bool ContainsList(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            return BulletListPattern.IsMatch(text) || NumberedListPattern.IsMatch(text);
        }

        /// <summary>
        /// Rendert Listeninhalt linksbündig mit korrekten Einrückungen.
        /// </summary>
        private void RenderListContent(ColumnDescriptor column, string textContent)
        {
            var lines = textContent.Split('\n');
            
            column.Item()
                .AlignLeft()
                .Column(innerCol =>
                {
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        // Einrückungstiefe ermitteln (Anzahl führender Leerzeichen / 2)
                        int leadingSpaces = line.TakeWhile(c => c == ' ' || c == '\t').Count();
                        float indentLevel = leadingSpaces / 2f;
                        
                        var trimmedLine = line.TrimStart();
                        
                        // Bindestrich durch Bullet-Point ersetzen
                        if (trimmedLine.StartsWith("- "))
                        {
                            trimmedLine = "• " + trimmedLine.Substring(2);
                        }
                        
                        innerCol.Item()
                            .PaddingLeft(indentLevel * 10) // 10pt pro Einrückungsebene
                            .Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(12 * _fontScale));
                                RenderFormattedText(text, trimmedLine);
                            });
                    }
                });
        }

        /// <summary>
        /// Repräsentiert einen Textabschnitt mit Formatierung.
        /// </summary>
        private class TextSegment
        {
            public string Text { get; set; } = "";
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsUnderline { get; set; }
            public bool IsHighlight { get; set; }
        }

        /// <summary>
        /// Rendert formatierten Text mit Markdown-Unterstützung.
        /// </summary>
        private void RenderFormattedText(TextDescriptor text, string content)
        {
            // Text in Segmente aufteilen
            var segments = ParseMarkdownSegments(content);
            
            foreach (var segment in segments)
            {
                var style = TextStyle.Default;
                
                if (segment.IsBold)
                    style = style.Bold();
                if (segment.IsItalic)
                    style = style.Italic();
                if (segment.IsUnderline)
                    style = style.Underline();
                if (segment.IsHighlight)
                    style = style.BackgroundColor(Colors.Yellow.Lighten3);
                
                text.Span(segment.Text).Style(style);
            }
        }

        /// <summary>
        /// Parst Markdown-Text in formatierte Segmente.
        /// </summary>
        private List<TextSegment> ParseMarkdownSegments(string text)
        {
            var segments = new List<TextSegment>();
            
            // Kombiniertes Pattern für alle Markdown-Formate
            var combinedPattern = new Regex(
                @"(\*\*(.+?)\*\*)|" +      // Bold: **text**
                @"(\*(.+?)\*)|" +           // Italic: *text*
                @"(__(.+?)__)|" +           // Underline: __text__
                @"(==(.+?)==)",             // Highlight: ==text==
                RegexOptions.Singleline);

            int lastIndex = 0;
            var matches = combinedPattern.Matches(text);

            foreach (Match match in matches)
            {
                // Text vor dem Match hinzufügen (unformatiert)
                if (match.Index > lastIndex)
                {
                    var plainText = text.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrEmpty(plainText))
                    {
                        segments.Add(new TextSegment { Text = plainText });
                    }
                }

                // Formatierten Text hinzufügen
                var segment = new TextSegment();
                
                if (match.Groups[1].Success) // Bold
                {
                    segment.Text = match.Groups[2].Value;
                    segment.IsBold = true;
                }
                else if (match.Groups[3].Success) // Italic
                {
                    segment.Text = match.Groups[4].Value;
                    segment.IsItalic = true;
                }
                else if (match.Groups[5].Success) // Underline
                {
                    segment.Text = match.Groups[6].Value;
                    segment.IsUnderline = true;
                }
                else if (match.Groups[7].Success) // Highlight
                {
                    segment.Text = match.Groups[8].Value;
                    segment.IsHighlight = true;
                }

                segments.Add(segment);
                lastIndex = match.Index + match.Length;
            }

            // Restlichen Text hinzufügen
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remainingText))
                {
                    segments.Add(new TextSegment { Text = remainingText });
                }
            }

            // Falls keine Segmente gefunden, den ganzen Text als unformatiert hinzufügen
            if (segments.Count == 0 && !string.IsNullOrEmpty(text))
            {
                segments.Add(new TextSegment { Text = text });
            }

            return segments;
        }

        /// <summary>
        /// Extrahiert Base64-Bilddaten aus dem Markdown-Text.
        /// </summary>
        private static List<string> ExtractImages(string text)
        {
            var images = new List<string>();
            var matches = ImageExtractPattern.Matches(text);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 3)
                {
                    images.Add(match.Groups[3].Value);
                }
            }
            
            return images;
        }
    }
}