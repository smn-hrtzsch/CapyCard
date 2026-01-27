using System.IO;
using System.Threading.Tasks;
using CapyCard.Services.ImportExport.Models;

namespace CapyCard.Services.ImportExport.Formats
{
    /// <summary>
    /// Interface für Format-spezifische Import/Export-Handler.
    /// </summary>
    public interface IFormatHandler
    {
        /// <summary>
        /// Unterstützte Dateiendung(en) für diesen Handler.
        /// </summary>
        string[] SupportedExtensions { get; }

        /// <summary>
        /// Anzeigename des Formats.
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// Beschreibung des Formats.
        /// </summary>
        string FormatDescription { get; }

        /// <summary>
        /// Prüft, ob der Handler auf der aktuellen Plattform verfügbar ist.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Analysiert eine Datei und gibt Informationen über den Inhalt zurück.
        /// </summary>
        /// <param name="stream">Datei-Stream</param>
        /// <param name="fileName">Dateiname für Format-Erkennung</param>
        /// <returns>Vorschau des Datei-Inhalts</returns>
        Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName);

        /// <summary>
        /// Importiert Karten aus einer Datei.
        /// </summary>
        /// <param name="stream">Datei-Stream</param>
        /// <param name="fileName">Dateiname</param>
        /// <param name="options">Import-Konfiguration</param>
        /// <returns>Import-Ergebnis mit Statistiken</returns>
        Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options);

        /// <summary>
        /// Exportiert Karten in einen Stream.
        /// </summary>
        /// <param name="stream">Ziel-Stream</param>
        /// <param name="options">Export-Konfiguration</param>
        /// <returns>Export-Ergebnis mit Statistiken</returns>
        Task<ExportResult> ExportAsync(Stream stream, ExportOptions options);
    }

    /// <summary>
    /// Vorschau-Informationen einer Import-Datei.
    /// </summary>
    public class ImportPreview
    {
        /// <summary>
        /// Analyse war erfolgreich.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Fehlermeldung bei Analyse-Fehler.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Erkanntes Format.
        /// </summary>
        public string? FormatName { get; set; }

        /// <summary>
        /// Name des Haupt-Decks.
        /// </summary>
        public string? DeckName { get; set; }

        /// <summary>
        /// Anzahl der gefundenen Karten.
        /// </summary>
        public int CardCount { get; set; }

        /// <summary>
        /// Anzahl der gefundenen Themen/Unterdecks.
        /// </summary>
        public int SubDeckCount { get; set; }

        /// <summary>
        /// Hat Lernfortschritt-Daten.
        /// </summary>
        public bool HasProgress { get; set; }

        /// <summary>
        /// Hat eingebettete Bilder.
        /// </summary>
        public bool HasMedia { get; set; }

        /// <summary>
        /// Erstellt eine erfolgreiche Vorschau.
        /// </summary>
        public static ImportPreview Successful(string formatName, string deckName, int cardCount, int subDeckCount)
        {
            return new ImportPreview
            {
                Success = true,
                FormatName = formatName,
                DeckName = deckName,
                CardCount = cardCount,
                SubDeckCount = subDeckCount
            };
        }

        /// <summary>
        /// Erstellt eine fehlerhafte Vorschau.
        /// </summary>
        public static ImportPreview Failed(string errorMessage)
        {
            return new ImportPreview
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
