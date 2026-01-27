using System.Collections.Generic;

namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Konfiguration für den Export von Kartenstapeln.
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Ausgabeformat (CapyCard, Anki, CSV).
        /// </summary>
        public ExportFormat Format { get; set; } = ExportFormat.CapyCard;

        /// <summary>
        /// Umfang des Exports (Ganzes Fach, Ausgewählte Themen, Ausgewählte Karten).
        /// </summary>
        public ExportScope Scope { get; set; } = ExportScope.FullDeck;

        /// <summary>
        /// ID des zu exportierenden Fachs.
        /// </summary>
        public int DeckId { get; set; }

        /// <summary>
        /// Lernfortschritt mit exportieren.
        /// </summary>
        public bool IncludeProgress { get; set; } = true;

        /// <summary>
        /// IDs der ausgewählten Themen (für SelectedSubDecks).
        /// </summary>
        public List<int>? SelectedSubDeckIds { get; set; }

        /// <summary>
        /// IDs der ausgewählten Karten (für SelectedCards).
        /// </summary>
        public List<int>? SelectedCardIds { get; set; }
    }

    /// <summary>
    /// Verfügbare Export-Formate.
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>CapyCard eigenes Format (.capycard ZIP+JSON).</summary>
        CapyCard,

        /// <summary>Anki-kompatibles Format (.apkg).</summary>
        Anki,

        /// <summary>CSV-Tabellenformat (.csv).</summary>
        Csv
    }

    /// <summary>
    /// Umfang des Exports.
    /// </summary>
    public enum ExportScope
    {
        /// <summary>Ganzes Fach inkl. aller Themen.</summary>
        FullDeck,

        /// <summary>Ausgewählte Themen/Unterdecks.</summary>
        SelectedSubDecks,

        /// <summary>Ausgewählte Karten.</summary>
        SelectedCards
    }
}
