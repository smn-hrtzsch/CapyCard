using FlashcardMobile.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlashcardMobile.Services.Pdf
{
    public class QuestPdfDocument : IDocument
    {
        private readonly List<Card> _cards;
        private readonly int _columnCount;
        private readonly float _fontScale;
        private readonly float _cellHeight;
        private readonly int _rowCount;

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
                                .Text(card.Front)
                                .FontSize(12 * _fontScale);
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
                                        .Text(card.Back)
                                        .FontSize(12 * _fontScale);
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
    }
}