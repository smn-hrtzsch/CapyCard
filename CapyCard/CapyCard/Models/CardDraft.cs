using System;

namespace CapyCard.Models
{
    public class CardDraft
    {
        public int DeckId { get; set; }

        public string Front { get; set; } = string.Empty;

        public string Back { get; set; } = string.Empty;

        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
