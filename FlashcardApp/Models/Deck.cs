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

        // Speichert den Index der zuletzt gelernten Karte in sequentieller Reihenfolge
        public int LastLearnedCardIndex { get; set; } = 0;

        // Speichert die IDs der im Shuffle-Modus gelernten Karten als JSON-String
        public string LearnedShuffleCardIdsJson { get; set; } = "[]";

        // Speichert den zuletzt verwendeten Lernmodus (Zufall oder nicht)
        public bool IsRandomOrder { get; set; } = false;
    }
}