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
    public partial class QuestPdfDocument : IDocument
    {
        private readonly List<Card> _cards;
        private readonly int _columnCount;
        private readonly float _fontScale;
        private readonly float _cellHeight;
        private readonly int _rowCount;

        // Regex-Patterns für Markdown
        [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
        private static partial Regex BoldPattern();
        
        [GeneratedRegex(@"\*(.+?)\*", RegexOptions.Compiled)]
        private static partial Regex ItalicPattern();
        
        [GeneratedRegex(@"__(.+?)__", RegexOptions.Compiled)]
        private static partial Regex UnderlinePattern();
        
        [GeneratedRegex(@"==(.+?)==", RegexOptions.Compiled)]
        private static partial Regex HighlightPattern();
        
        [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled)]
        private static partial Regex ImagePattern();

        public QuestPdfDocument(List<Card> cards, int columnCount)
        {
            _cards = cards;
            _columnCount = columnCount;

            // Basierend auf der Spaltenanzahl werden Zeilenanzahl, Schrifgröße und Zellenhöhe angepasst
            (_rowCount, _fontScale, _cellHeight) = columnCount switch
            {
                1 => (4, 1.5f, 6.5f),  // 4 Zeilen, 150% Schrift, 6.5cm Höhe
                2 => (5, 1.2f, 5.1f),  // 5 Zeilen, 120% Schrift, 5.1cm Höhe
                3 => (6, 1.0f, 4.2f),  // 6 Zeilen, 100% Schrift, 4.2cm Höhe
                4 => (6, 0.9f, 3.8f),  // 7 Zeilen, 90% Schrift, 3.7cm Höhe
                5 => (7, 0.8f, 3.3f),  // 8 Zeilen, 80% Schrift, 3.3cm Höhe
                _ => (6, 1.0f, 4.2f)   // Fallback auf 3 Spalten
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
                            table.Cell()
                                .Element(CardCellStyle)
                                .AlignCenter()
                                .AlignMiddle()
                                .ScaleToFit()
                                .Text(text => RenderMarkdownText(text, card.Front, 12 * _fontScale));
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
                                    cell
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .ScaleToFit()
                                        .Text(text => RenderMarkdownText(text, card.Back, 12 * _fontScale));
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

        /// <summary>
        /// Rendert Markdown-formatierten Text mit QuestPDF Text-Spans.
        /// Unterstützt: Fett (**), Kursiv (*), Unterstrichen (__), Hervorhebung (==).
        /// </summary>
        private void RenderMarkdownText(TextDescriptor text, string markdown, float fontSize)
        {
            if (string.IsNullOrEmpty(markdown))
                return;

            // Parse die Markdown-Segmente
            var segments = ParseMarkdownSegments(markdown);
            
            foreach (var segment in segments)
            {
                var span = text.Span(segment.Text);
                span.FontSize(fontSize);
                
                if (segment.IsBold)
                    span.Bold();
                if (segment.IsItalic)
                    span.Italic();
                if (segment.IsUnderline)
                    span.Underline();
                if (segment.IsHighlight)
                    span.BackgroundColor(Colors.Yellow.Lighten2);
            }
        }

        /// <summary>
        /// Parst Markdown in Segmente mit Formatierungsinformationen.
        /// </summary>
        private List<MarkdownSegment> ParseMarkdownSegments(string markdown)
        {
            var segments = new List<MarkdownSegment>();
            
            // Ersetze Bilder durch [Bild]-Platzhalter
            markdown = ImagePattern().Replace(markdown, "[Bild]");
            
            // Kombiniertes Pattern für alle Formatierungen
            var pattern = new Regex(@"(\*\*(.+?)\*\*)|(__(.+?)__)|(\*(.+?)\*)|(==(.+?)==)", RegexOptions.Compiled);
            
            int lastIndex = 0;
            foreach (Match match in pattern.Matches(markdown))
            {
                // Text vor dem Match hinzufügen
                if (match.Index > lastIndex)
                {
                    var beforeText = markdown.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrEmpty(beforeText))
                        segments.Add(new MarkdownSegment(beforeText));
                }
                
                // Formatierten Text hinzufügen
                if (match.Groups[1].Success) // Bold **text**
                {
                    segments.Add(new MarkdownSegment(match.Groups[2].Value, isBold: true));
                }
                else if (match.Groups[3].Success) // Underline __text__
                {
                    segments.Add(new MarkdownSegment(match.Groups[4].Value, isUnderline: true));
                }
                else if (match.Groups[5].Success) // Italic *text*
                {
                    segments.Add(new MarkdownSegment(match.Groups[6].Value, isItalic: true));
                }
                else if (match.Groups[7].Success) // Highlight ==text==
                {
                    segments.Add(new MarkdownSegment(match.Groups[8].Value, isHighlight: true));
                }
                
                lastIndex = match.Index + match.Length;
            }
            
            // Restlichen Text hinzufügen
            if (lastIndex < markdown.Length)
            {
                var remainingText = markdown.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remainingText))
                    segments.Add(new MarkdownSegment(remainingText));
            }
            
            // Falls keine Segmente gefunden wurden, den ganzen Text hinzufügen
            if (segments.Count == 0 && !string.IsNullOrEmpty(markdown))
            {
                segments.Add(new MarkdownSegment(markdown));
            }
            
            return segments;
        }
        
        private IContainer CardCellStyle(IContainer container)
        {
            return container
                .Border(1)
                .Padding(5)
                .Height(_cellHeight, Unit.Centimetre);
        }

        /// <summary>
        /// Repräsentiert ein Text-Segment mit Formatierungsinformationen.
        /// </summary>
        private class MarkdownSegment
        {
            public string Text { get; }
            public bool IsBold { get; }
            public bool IsItalic { get; }
            public bool IsUnderline { get; }
            public bool IsHighlight { get; }

            public MarkdownSegment(string text, bool isBold = false, bool isItalic = false, 
                bool isUnderline = false, bool isHighlight = false)
            {
                Text = text;
                IsBold = isBold;
                IsItalic = isItalic;
                IsUnderline = isUnderline;
                IsHighlight = isHighlight;
            }
        }
    }
}