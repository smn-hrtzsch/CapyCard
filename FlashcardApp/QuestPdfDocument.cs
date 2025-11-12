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
            // Pro Seite passen 7 Reihen à 3 Karten = 21 Karten.
            // Diese Zahl basiert auf der A4-Größe, den Rändern und der Kartenhöhe.
            const int cardsPerPage = 21;

            // Teilt die gesamte Kartenliste in Blöcke auf, die jeweils auf eine Seite passen.
            var cardChunks = _cards.Chunk(cardsPerPage);

            // Iteriert über jeden Block von Karten, um Vorder- und Rückseiten-Paare zu erstellen.
            foreach (var chunk in cardChunks)
            {
                var pageCards = chunk.ToList();

                // --- SEITE 1: VORDERSEITEN ---
                // Generiert eine Seite für die Vorderseiten der aktuellen Karten.
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        // Füllt die Tabelle mit den Vorderseiten der Karten.
                        foreach (var card in pageCards)
                        {
                            table.Cell()
                                .Element(CardCellStyle)
                                .AlignCenter()
                                .AlignMiddle()
                                .ScaleToFit()
                                .Text(card.Front);
                        }
                    });
                });

                // --- SEITE 2: RÜCKSEITEN ---
                // Bereitet die Rückseiten vor, indem die Reihenfolge für den Duplexdruck gespiegelt wird.
                var mirroredBacks = new List<Card?>();
                for (int i = 0; i < pageCards.Count; i += 3)
                {
                    var card1 = pageCards[i];
                    var card2 = (i + 1 < pageCards.Count) ? pageCards[i + 1] : null;
                    var card3 = (i + 2 < pageCards.Count) ? pageCards[i + 2] : null;

                    mirroredBacks.Add(card3);
                    mirroredBacks.Add(card2);
                    mirroredBacks.Add(card1);
                }

                // Generiert eine separate Seite für die Rückseiten.
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);

                    // Fügt einen kleinen oberen Abstand hinzu, um die Position der Rückseiten
                    // exakt an die der Vorderseiten für den Duplexdruck anzupassen.
                    page.Content()
                        .PaddingTop(2, Unit.Millimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            // Füllt die Tabelle mit den gespiegelten Rückseiten.
                            foreach (var card in mirroredBacks)
                            {
                                var cell = table.Cell().Element(CardCellStyle);

                                if (card != null)
                                {
                                    cell
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .ScaleToFit()
                                        .Text(card.Back);
                                }
                                else
                                {
                                    // Fügt eine leere Zelle hinzu, falls eine Reihe nicht vollständig ist.
                                    cell.Text(string.Empty);
                                }
                            }
                        });
                });
            }
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