namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Ergebnis eines Export-Vorgangs.
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Export war erfolgreich.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Fehlermeldung bei Misserfolg.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Pfad zur exportierten Datei (bei Erfolg).
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Exportierte Daten als Byte-Array (für Browser/Share).
        /// </summary>
        public byte[]? FileData { get; set; }

        /// <summary>
        /// Anzahl der exportierten Karten.
        /// </summary>
        public int CardsExported { get; set; }

        /// <summary>
        /// Anzahl der exportierten Themen.
        /// </summary>
        public int SubDecksExported { get; set; }

        /// <summary>
        /// Erstellt ein erfolgreiches ExportResult.
        /// </summary>
        public static ExportResult Successful(string filePath, int cardsExported, int subDecksExported)
        {
            return new ExportResult
            {
                Success = true,
                FilePath = filePath,
                CardsExported = cardsExported,
                SubDecksExported = subDecksExported
            };
        }

        /// <summary>
        /// Erstellt ein erfolgreiches ExportResult mit Byte-Daten.
        /// </summary>
        public static ExportResult SuccessfulWithData(byte[] data, int cardsExported, int subDecksExported)
        {
            return new ExportResult
            {
                Success = true,
                FileData = data,
                CardsExported = cardsExported,
                SubDecksExported = subDecksExported
            };
        }

        /// <summary>
        /// Erstellt ein fehlerhaftes ExportResult.
        /// </summary>
        public static ExportResult Failed(string errorMessage)
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
