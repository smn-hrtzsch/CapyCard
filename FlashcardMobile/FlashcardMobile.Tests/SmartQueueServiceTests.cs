using System;
using System.Collections.Generic;
using System.Linq;
using FlashcardMobile.Models;
using FlashcardMobile.Services;
using Xunit;

namespace FlashcardMobile.Tests
{
    public class SmartQueueServiceTests
    {
        private readonly SmartQueueService _service;

        public SmartQueueServiceTests()
        {
            _service = new SmartQueueService();
        }

        [Fact]
        public void CalculateNewScore_Rating1_DecreasesBoxByTwo()
        {
            // Case 1: 3 -> 1
            var score1 = new CardSmartScore { Score = 100, BoxIndex = 3, LastReviewed = DateTime.Now.AddDays(-1) };
            _service.CalculateNewScore(score1, 1);
            Assert.Equal(1, score1.BoxIndex);

            // Case 2: 1 -> 0 (Min)
            var score2 = new CardSmartScore { Score = 100, BoxIndex = 1 };
            _service.CalculateNewScore(score2, 1);
            Assert.Equal(0, score2.BoxIndex);
        }

        [Fact]
        public void CalculateNewScore_Rating2_DecreasesBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3 };
            _service.CalculateNewScore(score, 2);

            Assert.Equal(2, score.BoxIndex);
        }

        [Fact]
        public void CalculateNewScore_Rating3_IncreasesBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3 };
            _service.CalculateNewScore(score, 3);

            Assert.Equal(4, score.BoxIndex);
        }

        [Fact]
        public void CalculateNewScore_Rating4_IncreasesBoxByTwo()
        {
            // Case 1: 0 -> 2
            var score1 = new CardSmartScore { Score = 100, BoxIndex = 0 };
            _service.CalculateNewScore(score1, 4);
            Assert.Equal(2, score1.BoxIndex);

            // Case 2: 4 -> 5 (Max)
            var score2 = new CardSmartScore { Score = 100, BoxIndex = 4 };
            _service.CalculateNewScore(score2, 4);
            Assert.Equal(5, score2.BoxIndex);
        }

        [Fact]
        public void GetNextCard_ReturnsACard_WhenCardsAvailable()
        {
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "A" },
                new Card { Id = 2, Front = "B" }
            };
            var scores = new List<CardSmartScore>();

            var nextCard = _service.GetNextCard(cards, scores);

            Assert.NotNull(nextCard);
            Assert.Contains(cards, c => c.Id == nextCard!.Id);
        }

        [Fact]
        public void GetNextCard_ExcludesLastLearnedCard_WhenPossible()
        {
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "JustLearned" },
                new Card { Id = 2, Front = "Other" }
            };

            var now = DateTime.Now;
            var scores = new List<CardSmartScore>
            {
                new CardSmartScore { CardId = 1, BoxIndex = 0, LastReviewed = now }, // Just learned (latest)
                new CardSmartScore { CardId = 2, BoxIndex = 0, LastReviewed = now.AddMinutes(-5) } // Old
            };

            // Since both have Box 0 (same weight), the recent filter should force picking "Other".
            // We run it multiple times to be sure it's not just random luck.
            
            for (int i = 0; i < 10; i++)
            {
                var nextCard = _service.GetNextCard(cards, scores);
                Assert.Equal(2, nextCard!.Id);
            }
        }

        [Fact]
        public void GetNextCard_PicksLastLearned_IfOnlyOneCard()
        {
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "JustLearned" }
            };

            var now = DateTime.Now;
            var scores = new List<CardSmartScore>
            {
                new CardSmartScore { CardId = 1, BoxIndex = 0, LastReviewed = now }
            };

            var nextCard = _service.GetNextCard(cards, scores);

            Assert.NotNull(nextCard);
            Assert.Equal(1, nextCard!.Id);
        }
        
        [Fact]
        public void GetNextCard_DistributionCheck_HardCardsMoreFrequent()
        {
            // This is a probabilistic test. We simulate many draws and check if Box 0 is picked more often than Box 5.
            // We add a 3rd card that was "just learned" to ensure it gets excluded, leaving the other two to compete.
            
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "Hard" },
                new Card { Id = 2, Front = "Easy" },
                new Card { Id = 3, Front = "JustFinished" }
            };

            var now = DateTime.Now;
            var scores = new List<CardSmartScore>
            {
                new CardSmartScore { CardId = 1, BoxIndex = 0, LastReviewed = now.AddMinutes(-10) }, // Weight 100
                new CardSmartScore { CardId = 2, BoxIndex = 5, LastReviewed = now.AddMinutes(-10) }, // Weight 3
                new CardSmartScore { CardId = 3, BoxIndex = 0, LastReviewed = now } // Latest, will be excluded
            };

            int hardCount = 0;
            int easyCount = 0;
            int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                var nextCard = _service.GetNextCard(cards, scores);
                if (nextCard!.Id == 1) hardCount++;
                else if (nextCard.Id == 2) easyCount++;
            }

            // Expected ratio is roughly 100:15 (~6.6:1).
            // Hard should be significantly more frequent.
            Assert.True(hardCount > easyCount * 5, $"Hard count ({hardCount}) should be much higher than Easy count ({easyCount})");
        }

        [Fact]
        public void Simulation_1000Iterations_CheckDistribution()
        {
            // Setup 10 cards
            var cards = new List<Card>();
            var scores = new List<CardSmartScore>();
            
            for (int i = 0; i < 10; i++)
            {
                cards.Add(new Card { Id = i, Front = $"Card {i}" });
                scores.Add(new CardSmartScore { CardId = i, BoxIndex = 0, LastReviewed = DateTime.MinValue });
            }

            // Categories
            // 0-2: Easy (4) - 3 Cards
            // 3-5: Good (3) - 3 Cards
            // 6-7: Hard (2) - 2 Cards
            // 8-9: Again (1) - 2 Cards

            var counts = new Dictionary<int, int>();
            for (int i = 0; i < 10; i++) counts[i] = 0;

            for (int i = 0; i < 1000; i++)
            {
                var card = _service.GetNextCard(cards, scores);
                Assert.NotNull(card);
                
                counts[card!.Id]++;
                
                var score = scores.First(s => s.CardId == card.Id);
                int rating = 0;
                
                if (card.Id <= 2) rating = 4; // Easy
                else if (card.Id <= 5) rating = 3; // Good
                else if (card.Id <= 7) rating = 2; // Hard
                else rating = 1; // Again

                _service.CalculateNewScore(score, rating);
            }

            // Analyze results
            System.Console.WriteLine("--- DETAILED CARD STATS (1000 Iterations) ---");
            foreach (var kvp in counts.OrderBy(x => x.Key))
            {
                string category = "";
                if (kvp.Key <= 2) category = "Easy (4)";
                else if (kvp.Key <= 5) category = "Good (3)";
                else if (kvp.Key <= 7) category = "Hard (2)";
                else category = "Again (1)";

                System.Console.WriteLine($"Card {kvp.Key} [{category}]: {kvp.Value} times");
            }
            System.Console.WriteLine("---------------------------------------------");

            int easyCount = counts.Where(k => k.Key <= 2).Sum(k => k.Value);
            int goodCount = counts.Where(k => k.Key >= 3 && k.Key <= 5).Sum(k => k.Value);
            int hardCount = counts.Where(k => k.Key >= 6 && k.Key <= 7).Sum(k => k.Value);
            int againCount = counts.Where(k => k.Key >= 8).Sum(k => k.Value);

            double avgEasy = easyCount / 3.0;
            double avgGood = goodCount / 3.0;
            double avgHard = hardCount / 2.0;
            double avgAgain = againCount / 2.0;

            System.Console.WriteLine($"SUMMARY: Easy Avg: {avgEasy:F1}, Good Avg: {avgGood:F1}, Hard Avg: {avgHard:F1}, Again Avg: {avgAgain:F1}");

            // Assertions
            Assert.True(avgAgain > avgGood, $"Again ({avgAgain}) should be > Good ({avgGood})");
            Assert.True(avgHard > avgGood, $"Hard ({avgHard}) should be > Good ({avgGood})");
            Assert.True(avgGood >= avgEasy, $"Good ({avgGood}) should be >= Easy ({avgEasy})");
        }

        [Fact]
        public void Simulation_ManyHardFewEasy_CheckDistribution()
        {
            // Scenario: 8 Hard cards, 2 Easy cards.
            // We expect the user to spend almost all time on the hard cards.
            
            var cards = new List<Card>();
            var scores = new List<CardSmartScore>();
            
            for (int i = 0; i < 10; i++)
            {
                cards.Add(new Card { Id = i, Front = $"Card {i}" });
                scores.Add(new CardSmartScore { CardId = i, BoxIndex = 0, LastReviewed = DateTime.MinValue });
            }

            var counts = new Dictionary<int, int>();
            for (int i = 0; i < 10; i++) counts[i] = 0;

            for (int i = 0; i < 1000; i++)
            {
                var card = _service.GetNextCard(cards, scores);
                Assert.NotNull(card);
                counts[card!.Id]++;
                
                var score = scores.First(s => s.CardId == card.Id);
                
                // 0-7: Hard (Rate 2)
                // 8-9: Easy (Rate 4)
                int rating = (card.Id <= 7) ? 2 : 4;
                _service.CalculateNewScore(score, rating);
            }

            System.Console.WriteLine("--- SCENARIO: MANY HARD (8), FEW EASY (2) ---");
            int hardTotal = counts.Where(k => k.Key <= 7).Sum(k => k.Value);
            int easyTotal = counts.Where(k => k.Key >= 8).Sum(k => k.Value);
            
            double avgHard = hardTotal / 8.0;
            double avgEasy = easyTotal / 2.0;

            System.Console.WriteLine($"Hard Total: {hardTotal} (Avg {avgHard:F1})");
            System.Console.WriteLine($"Easy Total: {easyTotal} (Avg {avgEasy:F1})");
            System.Console.WriteLine($"Ratio Hard/Easy: {avgHard/avgEasy:F2}");

            // With 8 hard cards (weight 100 each) and 2 easy cards (weight ~15 each),
            // the hard cards should dominate massively.
            Assert.True(avgHard > avgEasy * 5, "Hard cards should be shown much more frequently on average.");
        }

        [Fact]
        public void Simulation_FewHardManyEasy_CheckDistribution()
        {
            // Scenario: 2 Hard cards, 8 Easy cards.
            // Even though there are many easy cards, the few hard ones should still appear frequently.
            
            var cards = new List<Card>();
            var scores = new List<CardSmartScore>();
            
            for (int i = 0; i < 10; i++)
            {
                cards.Add(new Card { Id = i, Front = $"Card {i}" });
                scores.Add(new CardSmartScore { CardId = i, BoxIndex = 0, LastReviewed = DateTime.MinValue });
            }

            var counts = new Dictionary<int, int>();
            for (int i = 0; i < 10; i++) counts[i] = 0;

            for (int i = 0; i < 1000; i++)
            {
                var card = _service.GetNextCard(cards, scores);
                Assert.NotNull(card);
                counts[card!.Id]++;
                
                var score = scores.First(s => s.CardId == card.Id);
                
                // 0-1: Hard (Rate 2)
                // 2-9: Easy (Rate 4)
                int rating = (card.Id <= 1) ? 2 : 4;
                _service.CalculateNewScore(score, rating);
            }

            System.Console.WriteLine("--- SCENARIO: FEW HARD (2), MANY EASY (8) ---");
            int hardTotal = counts.Where(k => k.Key <= 1).Sum(k => k.Value);
            int easyTotal = counts.Where(k => k.Key >= 2).Sum(k => k.Value);
            
            double avgHard = hardTotal / 2.0;
            double avgEasy = easyTotal / 8.0;

            System.Console.WriteLine($"Hard Total: {hardTotal} (Avg {avgHard:F1})");
            System.Console.WriteLine($"Easy Total: {easyTotal} (Avg {avgEasy:F1})");
            System.Console.WriteLine($"Ratio Hard/Easy: {avgHard/avgEasy:F2}");

            // Even with few hard cards, they should be prioritized.
            // Hard weight ~100, Easy weight ~15.
            // Ratio should be around 6.6.
            Assert.True(avgHard > avgEasy * 4, "Hard cards should still be shown more frequently on average.");
        }
    }
}
