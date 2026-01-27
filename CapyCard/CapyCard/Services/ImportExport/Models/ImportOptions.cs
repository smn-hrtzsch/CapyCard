namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Konfiguration für den Import von Kartenstapeln.
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// Ziel des Imports (Neues Fach, Bestehendes Fach, Als Thema).
        /// </summary>
        public ImportTarget Target { get; set; } = ImportTarget.NewDeck;

        /// <summary>
        /// ID des Ziel-Decks (für ExistingDeck/ExistingSubDeck).
        /// </summary>
        public int? TargetDeckId { get; set; }

        /// <summary>
        /// Name des neuen Fachs (für NewDeck).
        /// </summary>
        public string? NewDeckName { get; set; }

        /// <summary>
        /// Lernfortschritt mit importieren.
        /// </summary>
        public bool IncludeProgress { get; set; } = true;

        /// <summary>
        /// Verhalten bei doppelten Karten.
        /// </summary>
        public DuplicateHandling OnDuplicate { get; set; } = DuplicateHandling.KeepBoth;
    }

    /// <summary>
    /// Ziel des Imports.
    /// </summary>
    public enum ImportTarget
    {
        /// <summary>Neues Fach erstellen.</summary>
        NewDeck,

        /// <summary>In bestehendes Fach importieren.</summary>
        ExistingDeck,

        /// <summary>Als Thema (Unterdeck) in ein bestehendes Fach importieren.</summary>
        ExistingSubDeck
    }

    /// <summary>
    /// Verhalten bei doppelten Karten.
    /// </summary>
    public enum DuplicateHandling
    {
        /// <summary>Doppelte Karten überspringen.</summary>
        Skip,

        /// <summary>Vorhandene Karten ersetzen.</summary>
        Replace,

        /// <summary>Beide Versionen behalten.</summary>
        KeepBoth
    }
}
