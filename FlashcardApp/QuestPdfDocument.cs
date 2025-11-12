using FlashcardApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace FlashcardApp
{
    /// <summary>
    /// Definiert das Layout für unser PDF-Dokument (DIN-A4, 3 Spalten, Duplex).
    /// </summary>
    public class QuestPdfDocument : IDocument
    {
        private readonly List<Card> _cards;
        
        public QuestPdfDocument(List<Card> cards)
        {
            _cards = cards;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        // Diese Methode definiert das gesamte Layout
        public void Compose(IDocumentContainer container)
        {
            // 1. Erstelle die gespiegelte Liste für 3 Spalten (unverändert)
            var mirroredBacks = new List<Card?>();
            for (int i = 0; i < _cards.Count; i += 3)
            {
                var card1 = _cards[i];
                var card2 = (i + 1 < _cards.Count) ? _cards[i + 1] : null;
                var card3 = (i + 2 < _cards.Count) ? _cards[i + 2] : null;

                mirroredBacks.Add(card3); 
                mirroredBacks.Add(card2); 
                mirroredBacks.Add(card1); 
            }
            
            const int maxLines = 6; // Basiswert: Annahme, dass 6 Zeilen optimal passen

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                
                page.Content()
                    .Column(column => 
                    {
                        // (A) VORDERSEITEN-Layout
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            foreach (var card in _cards)
                            {
                                var text = card.Front;
                                var actualLines = text.Split('\n').Length;
    
                                float scaleFactor = 1.0f;
                                if (actualLines > maxLines)
                                {
                                    scaleFactor = (float)maxLines / actualLines;
                                }

                                table.Cell()
                                    .Element(CardCellStyle)
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .Scale(scaleFactor)
                                    .Text(text);
                            }
                        });

                        // (B) SEITENUMBRUCH (unverändert)
                        column.Item().PageBreak();

                        // (C) RÜCKSEITEN-Layout
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            foreach (var card in mirroredBacks)
                            {
                                var cell = table.Cell().Element(CardCellStyle);
                                
                                if (card != null)
                                {
                                    var text = card.Back;
                                    var actualLines = text.Split('\n').Length;
    
                                    float scaleFactor = 1.0f;
                                    if (actualLines > maxLines)
                                    {
                                        scaleFactor = (float)maxLines / actualLines;
                                    }

                                    cell
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Scale(scaleFactor)
                                        .Text(text);
                                }
                                else
                                {
                                    cell.Text(string.Empty);
                                }
                            }
                        });
                    });
            });
        }
        
        /// <summary>
        /// Stil für die PDF-Zellen.
        /// </summary>
        static IContainer CardCellStyle(IContainer container)
        {
            // Padding 5, Höhe 3cm (unverändert)
            return container
                .Border(1)
                .Padding(5) 
                .Height(3, Unit.Centimetre);
        }
    }
}