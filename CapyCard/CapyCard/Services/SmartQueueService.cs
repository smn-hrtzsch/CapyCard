using System;
using System.Collections.Generic;
using System.Linq;
using CapyCard.Models;

namespace CapyCard.Services
{
    public class SmartQueueService
    {
        private readonly Random _random = new Random();

        // Ratings: 1 (Nochmal), 2 (Schwer), 3 (Gut), 4 (Einfach)
        public void CalculateNewScore(CardSmartScore smartScore, int rating)
        {
            // Update LastReviewed
            smartScore.LastReviewed = DateTime.Now;

            // Wir nutzen primär den BoxIndex (0-5) für die Gewichtung.
            // Score ist nur noch sekundär oder für Statistiken.
            switch (rating)
            {
                case 1: // Nochmal (Fail)
                    // User request: Decrease by 2 instead of reset to 0.
                    smartScore.BoxIndex -= 2;
                    if (smartScore.BoxIndex < 0) smartScore.BoxIndex = 0;
                    
                    smartScore.Score = 0;
                    break;
                case 2: // Schwer (Hard)
                    if (smartScore.BoxIndex > 0) smartScore.BoxIndex--;
                    break;
                case 3: // Gut (Good)
                    if (smartScore.BoxIndex < 5) smartScore.BoxIndex++;
                    break;
                case 4: // Einfach (Easy)
                    // User request: Jump 2 boxes (was 3).
                    smartScore.BoxIndex += 2;
                    if (smartScore.BoxIndex > 5) smartScore.BoxIndex = 5;
                    break;
                default:
                    break;
            }
        }

        public Card? GetNextCard(List<Card> cards, List<CardSmartScore> scores)
        {
            var now = DateTime.Now;

            // 1. Kandidaten vorbereiten mit BoxIndex und Zeit
            var candidates = cards.Select(card => 
            {
                var score = scores.FirstOrDefault(s => s.CardId == card.Id);
                return new 
                { 
                    Card = card, 
                    BoxIndex = score?.BoxIndex ?? 0, // Neue Karten sind Box 0
                    LastReviewed = score?.LastReviewed ?? DateTime.MinValue 
                };
            }).ToList();

            if (!candidates.Any()) return null;

            // 2. "Recent"-Filter: Die zuletzt gelernte Karte ausschließen,
            //    um direkte Wiederholung zu vermeiden (außer es gibt nur eine Karte).
            var validCandidates = candidates.ToList();

            if (candidates.Count > 1)
            {
                // Die Karte mit dem neuesten Datum ist die, die gerade bewertet wurde.
                var lastLearned = candidates.OrderByDescending(c => c.LastReviewed).First();

                // Nur ausschließen, wenn sie tatsächlich schon mal gelernt wurde (LastReviewed > MinValue)
                if (lastLearned.LastReviewed > DateTime.MinValue)
                {
                    validCandidates = candidates.Where(c => c.Card.Id != lastLearned.Card.Id).ToList();
                }
            }

            // 3. Gewichtung berechnen (Weighted Random Selection)
            //    Je niedriger die Box, desto höher die Wahrscheinlichkeit.
            var weightedCandidates = validCandidates.Select(c => new 
            {
                c.Card,
                Weight = GetWeight(c.BoxIndex)
            }).ToList();

            // 4. Zufällige Auswahl basierend auf Gewichtung
            double totalWeight = weightedCandidates.Sum(c => c.Weight);
            double randomValue = _random.NextDouble() * totalWeight;

            double currentSum = 0;
            foreach (var item in weightedCandidates)
            {
                currentSum += item.Weight;
                if (randomValue <= currentSum)
                {
                    return item.Card;
                }
            }

            // Fallback (sollte mathematisch nicht erreicht werden)
            return weightedCandidates.Last().Card;
        }

        private double GetWeight(int boxIndex)
        {
            // Adjusted weights to achieve approx. 80% focus on hard cards
            // Box 0 (Neu/Schwer): 100 Lose
            // Box 5 (Perfekt): 15 Lose
            // Ratio 100:15 is approx 6.6:1
            
            switch (boxIndex)
            {
                case 0: return 100.0;
                case 1: return 75.0;
                case 2: return 50.0;
                case 3: return 30.0;
                case 4: return 20.0;
                case 5: return 15.0;
                default: return 1.0;
            }
        }
    }
}
