using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapyCard.Models
{
    /// <summary>
    /// Repräsentiert ein Bild, das einer Karte zugeordnet ist.
    /// Bilder werden als Base64-Daten in der Datenbank gespeichert.
    /// </summary>
    public class CardImage
    {
        public int Id { get; set; }

        /// <summary>
        /// Eindeutiger Identifier für das Bild, wird im Markdown referenziert.
        /// Format: "img-{guid}"
        /// </summary>
        [Required]
        public string ImageId { get; set; } = string.Empty;

        /// <summary>
        /// Base64-kodierte Bilddaten.
        /// </summary>
        [Required]
        public string Base64Data { get; set; } = string.Empty;

        /// <summary>
        /// MIME-Typ des Bildes (z.B. "image/png", "image/jpeg").
        /// </summary>
        [Required]
        public string MimeType { get; set; } = "image/png";

        /// <summary>
        /// Optionaler Dateiname für Anzeigezwecke.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Fremdschlüssel zur zugehörigen Karte.
        /// </summary>
        public int CardId { get; set; }

        [ForeignKey("CardId")]
        public virtual Card Card { get; set; } = null!;
    }
}
