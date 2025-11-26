using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapyCard.Models
{
    public class CardSmartScore
    {
        [Key]
        public int Id { get; set; }

        public int CardId { get; set; }
        
        [ForeignKey(nameof(CardId))]
        public Card? Card { get; set; }

        /// <summary>
        /// Determines the position in the queue. Lower value = Higher priority.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Used to break ties in Score (older reviews come first).
        /// </summary>
        public DateTime LastReviewed { get; set; }

        /// <summary>
        /// 0 to 5 (Leitner-Box logic). 0 = New/Forgotten, 5 = Mastered.
        /// </summary>
        public int BoxIndex { get; set; }
    }
}
