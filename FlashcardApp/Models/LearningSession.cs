using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace FlashcardApp.Models
{
    public enum LearningMode
    {
        MainOnly,
        AllRecursive,
        CustomSelection
    }

    public class LearningSession
    {
        public int Id { get; set; }

        public int DeckId { get; set; }
        [ForeignKey("DeckId")]
        public virtual Deck Deck { get; set; } = null!;

        public LearningMode Mode { get; set; }

        // For Custom mode: which subdecks are included (JSON list of ints)
        // Also used to verify if the selection changed.
        public string SelectedDeckIdsJson { get; set; } = "[]";

        // Progress
        public int LastLearnedIndex { get; set; } = 0;
        public string LearnedCardIdsJson { get; set; } = "[]"; // For random mode
        public bool IsRandomOrder { get; set; } = false;

        public DateTime LastAccessed { get; set; } = DateTime.Now;
    }
}
