using System;
using System.Collections.Generic;
using System.Linq;
using FlashcardMobile.Models;

namespace FlashcardMobile.Services
{
    public class SmartQueueService
    {
        // Ratings: 1 (Nochmal), 2 (Schwer), 3 (Gut), 4 (Einfach)
        
        public void CalculateNewScore(CardSmartScore smartScore, int rating)
        {
            // Update LastReviewed
            smartScore.LastReviewed = DateTime.Now;

            switch (rating)
            {
                case 1: // Nochmal
                    smartScore.Score = 0; // Immediate review
                    smartScore.BoxIndex = 0;
                    break;
                case 2: // Schwer
                    smartScore.Score += 10; // Small increase
                    // BoxIndex stays same or decreases? Let's keep it same or decrease if > 0
                    if (smartScore.BoxIndex > 0) smartScore.BoxIndex--;
                    break;
                case 3: // Gut
                    smartScore.Score += 50; // Moderate increase
                    if (smartScore.BoxIndex < 5) smartScore.BoxIndex++;
                    break;
                case 4: // Einfach
                    smartScore.Score += 200; // Large increase
                    if (smartScore.BoxIndex < 5) smartScore.BoxIndex++;
                    break;
                default:
                    break;
            }
        }

        public Card? GetNextCard(List<Card> cards, List<CardSmartScore> scores)
        {
            // Join cards with scores
            // If a card has no score, treat it as new (Score 0)
            
            var query = from c in cards
                        join s in scores on c.Id equals s.CardId into joined
                        from subScore in joined.DefaultIfEmpty()
                        select new { Card = c, Score = subScore };

            // Sort by Score (asc), then LastReviewed (asc)
            // Null score means new card, so priority 0 (or very high priority)
            
            var nextItem = query
                .OrderBy(x => x.Score == null ? 0 : x.Score.Score)
                .ThenBy(x => x.Score == null ? DateTime.MinValue : x.Score.LastReviewed)
                .FirstOrDefault();

            return nextItem?.Card;
        }
    }
}
