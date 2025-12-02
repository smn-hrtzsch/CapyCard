using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapyCard.Models
{
    /// <summary>
    /// Die 'Card' repräsentiert eine einzelne Karteikarte.
    /// Vorder- und Rückseite werden als Markdown gespeichert für Rich-Text-Formatierung.
    /// </summary>
    public class Card
    {
        public int Id { get; set; } // Primärschlüssel

        /// <summary>
        /// Vorderseite der Karte im Markdown-Format.
        /// Unterstützt: **Fett**, *Kursiv*, __Unterstrichen__, Listen, Bilder.
        /// </summary>
        [Required]
        public string Front { get; set; } = string.Empty;

        /// <summary>
        /// Rückseite der Karte im Markdown-Format.
        /// </summary>
        [Required]
        public string Back { get; set; } = string.Empty;

        // Fremdschlüssel: Sagt EF Core, dass diese Karte
        // zu einem 'Deck' gehört.
        public int DeckId { get; set; }

        [ForeignKey("DeckId")]
        public virtual Deck Deck { get; set; } = null!;

        /// <summary>
        /// Bilder, die in dieser Karte referenziert werden.
        /// </summary>
        public virtual ICollection<CardImage> Images { get; set; } = new List<CardImage>();
    }
}