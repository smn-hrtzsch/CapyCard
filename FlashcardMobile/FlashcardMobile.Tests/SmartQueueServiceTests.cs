using System;
using System.Collections.Generic;
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
        public void CalculateNewScore_Rating1_ResetsScoreAndBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3, LastReviewed = DateTime.Now.AddDays(-1) };
            _service.CalculateNewScore(score, 1);

            Assert.Equal(0, score.Score);
            Assert.Equal(0, score.BoxIndex);
            Assert.True(score.LastReviewed > DateTime.Now.AddMinutes(-1));
        }

        [Fact]
        public void CalculateNewScore_Rating2_IncreasesScoreSlightly_DecreasesBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3 };
            _service.CalculateNewScore(score, 2);

            Assert.Equal(110, score.Score);
            Assert.Equal(2, score.BoxIndex);
        }

        [Fact]
        public void CalculateNewScore_Rating3_IncreasesScoreModerately_IncreasesBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3 };
            _service.CalculateNewScore(score, 3);

            Assert.Equal(150, score.Score);
            Assert.Equal(4, score.BoxIndex);
        }

        [Fact]
        public void CalculateNewScore_Rating4_IncreasesScoreSignificantly_IncreasesBox()
        {
            var score = new CardSmartScore { Score = 100, BoxIndex = 3 };
            _service.CalculateNewScore(score, 4);

            Assert.Equal(300, score.Score);
            Assert.Equal(4, score.BoxIndex);
        }

        [Fact]
        public void GetNextCard_ReturnsCardWithLowestScore()
        {
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "A" },
                new Card { Id = 2, Front = "B" },
                new Card { Id = 3, Front = "C" }
            };

            var scores = new List<CardSmartScore>
            {
                new CardSmartScore { CardId = 1, Score = 100, LastReviewed = DateTime.Now },
                new CardSmartScore { CardId = 2, Score = 10, LastReviewed = DateTime.Now },
                new CardSmartScore { CardId = 3, Score = 50, LastReviewed = DateTime.Now }
            };

            var nextCard = _service.GetNextCard(cards, scores);

            Assert.NotNull(nextCard);
            Assert.Equal(2, nextCard!.Id); // Score 10 is lowest
        }

        [Fact]
        public void GetNextCard_PrioritizesNewCards()
        {
            var cards = new List<Card>
            {
                new Card { Id = 1, Front = "A" },
                new Card { Id = 2, Front = "B" } // No score
            };

            var scores = new List<CardSmartScore>
            {
                new CardSmartScore { CardId = 1, Score = 10, LastReviewed = DateTime.Now }
            };

            var nextCard = _service.GetNextCard(cards, scores);

            Assert.NotNull(nextCard);
            Assert.Equal(2, nextCard!.Id); // No score means score 0 (effectively)
        }
    }
}
