using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FlashcardApp.Models
{
    // Das 'Deck' repräsentiert ein "Fach" oder einen "Stapel"
    public class Deck
    {
        public int Id { get; set; } // Primärschlüssel

        [Required] // Sorgt dafür, dass ein Name vorhanden sein muss
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Ein Fach kann viele Karten haben (1-zu-N-Beziehung)
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
    }
}