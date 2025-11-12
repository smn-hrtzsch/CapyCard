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
            // 1. KORREKTUR: Erstelle die gespiegelte Liste für 3 Spalten
            var mirroredBacks = new List<Card?>();
            for (int i = 0; i < _cards.Count; i += 3) // In 3er-Schritten vorgehen
            {
                var card1 = _cards[i];
                var card2 = (i + 1 < _cards.Count) ? _cards[i + 1] : null;
                var card3 = (i + 2 < _cards.Count) ? _cards[i + 2] : null;

                // In umgekehrter Reihenfolge für Duplex-Spiegelung hinzufügen
                mirroredBacks.Add(card3); 
                mirroredBacks.Add(card2); 
                mirroredBacks.Add(card1); 
            }
            
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
                            // KORREKTUR: 3 Spalten
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn(); // NEU
                            });
                            
                            foreach (var card in _cards)
                            {
                                // KORREKTUR (Zentrierung):
                                // .AlignCenter() auf der Zelle zentriert den Inhalt horizontal.
                                // .AlignCenter() auf dem Text zentriert mehrzeiligen Text in sich selbst.
                                table.Cell()
                                    .Element(CardCellStyle)
                                    .AlignMiddle()  // Vertikal
                                    .AlignCenter()  // Horizontal (FIX)
                                    .Shrink()
                                    .Text(card.Front)
                                    .AlignCenter(); // Text-interne Zentrierung
                            }
                        });

                        // (B) SEITENUMBRUCH (unverändert)
                        column.Item().PageBreak();

                        // (C) RÜCKSEITEN-Layout
                        column.Item().Table(table =>
                        {
                            // KORREKTUR: 3 Spalten
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn(); // NEU
                            });
                            
                            foreach (var card in mirroredBacks)
                            {
                                var cell = table.Cell().Element(CardCellStyle);
                                
                                if (card != null)
                                {
                                    // KORREKTUR: Identische Zentrierungslogik
                                    cell
                                        .AlignMiddle()
                                        .AlignCenter()
                                        .Shrink()
                                        .Text(card.Back)
                                        .AlignCenter();
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
            // KORREKTUR: Höhe auf 3cm geändert
            return container
                .Border(1)
                .Padding(10) 
                .Height(3, Unit.Centimetre);
        }
    }
}