using FlashcardApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace FlashcardApp
{
    /// <summary>
    /// Definiert das Layout für unser PDF-Dokument (DIN-A4, 2 Spalten, Duplex).
    /// </summary>
    public class QuestPdfDocument : IDocument
    {
        private readonly List<Card> _cards;
        
        // Definiert die Farbe, die wir als "unsichtbar" verwenden
        private static readonly string _invisibleColor = Colors.White;

        public QuestPdfDocument(List<Card> cards)
        {
            _cards = cards;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        // Diese Methode definiert das gesamte Layout
        public void Compose(IDocumentContainer container)
        {
            // 1. Erstelle die gespiegelte Liste der Rückseiten (unverändert)
            var mirroredBacks = new List<Card?>();
            for (int i = 0; i < _cards.Count; i += 2)
            {
                var card1 = _cards[i];
                var card2 = (i + 1 < _cards.Count) ? _cards[i + 1] : null;

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
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            foreach (var card in _cards)
                            {
                                // KORREKTUR: Wir verwenden 'Layers' (Overlay) statt 'Column' (Stapel)
                                table.Cell()
                                    .Element(CardCellStyle)
                                    .Layers(layers =>
                                    {
                                        // Layer 1: Der "Mess-Layer" (unsichtbar)
                                        // Rendert die Rückseite, um deren Höhe zu messen.
                                        layers.Layer()
                                            .DefaultTextStyle(x => x.FontColor(_invisibleColor))
                                            .Text(card.Back);
                                        
                                        // Layer 2: Der "Inhalts-Layer" (sichtbar)
                                        // KORREKTUR: Wird als 'PrimaryLayer' markiert,
                                        //            behebt den Runtime-Crash.
                                        layers.PrimaryLayer()
                                            .Text(card.Front);
                                    });
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
                            });
                            
                            foreach (var card in mirroredBacks)
                            {
                                var cell = table.Cell().Element(CardCellStyle);
                                
                                if (card != null)
                                {
                                    // KORREKTUR: Identische 'Layers'-Logik
                                    cell.Layers(layers =>
                                    {
                                        // Layer 1: Der "Mess-Layer" (unsichtbar)
                                        layers.Layer()
                                            .DefaultTextStyle(x => x.FontColor(_invisibleColor))
                                            .Text(card.Front);
                                        
                                        // Layer 2: Der "Inhalts-Layer" (sichtbar)
                                        // KORREKTUR: Wird als 'PrimaryLayer' markiert.
                                        layers.PrimaryLayer()
                                            .Text(card.Back);
                                    });
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
        /// Stil für die PDF-Zellen (unverändert).
        /// </summary>
        static IContainer CardCellStyle(IContainer container)
        {
            return container
                .Border(1)
                .Padding(10) 
                .PaddingBottom(1, Unit.Centimetre); 
        }
    }
}