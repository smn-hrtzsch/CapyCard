using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlashcardMobile.Models
{
    // Die 'Card' repräsentiert eine einzelne Karteikarte
    public class Card
    {
        public int Id { get; set; } // Primärschlüssel

        [Required]
        public string Front { get; set; } = string.Empty; // Vorderseite

        [Required]
        public string Back { get; set; } = string.Empty; // Rückseite

        // Fremdschlüssel: Sagt EF Core, dass diese Karte
        // zu einem 'Deck' gehört.
        public int DeckId { get; set; }

        [ForeignKey("DeckId")]
        public virtual Deck Deck { get; set; } = null!;
    }
}