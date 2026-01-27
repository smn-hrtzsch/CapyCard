using System.Collections.Generic;

namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Ergebnis eines Import-Vorgangs mit Statistiken.
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// Import war erfolgreich.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Fehlermeldung bei Misserfolg.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Anzahl der importierten Karten.
        /// </summary>
        public int CardsImported { get; set; }

        /// <summary>
        /// Anzahl der übersprungenen Karten (bei Duplikaten).
        /// </summary>
        public int CardsSkipped { get; set; }

        /// <summary>
        /// Anzahl der aktualisierten Karten (bei Replace).
        /// </summary>
        public int CardsUpdated { get; set; }

        /// <summary>
        /// Anzahl der erstellten Themen/Unterdecks.
        /// </summary>
        public int SubDecksCreated { get; set; }

        /// <summary>
        /// ID des erstellten oder verwendeten Ziel-Decks.
        /// </summary>
        public int? TargetDeckId { get; set; }

        /// <summary>
        /// Warnungen während des Imports.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Erstellt ein erfolgreiches ImportResult.
        /// </summary>
        public static ImportResult Successful(int cardsImported, int subDecksCreated, int targetDeckId)
        {
            return new ImportResult
            {
                Success = true,
                CardsImported = cardsImported,
                SubDecksCreated = subDecksCreated,
                TargetDeckId = targetDeckId
            };
        }

        /// <summary>
        /// Erstellt ein fehlerhaftes ImportResult.
        /// </summary>
        public static ImportResult Failed(string errorMessage)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
