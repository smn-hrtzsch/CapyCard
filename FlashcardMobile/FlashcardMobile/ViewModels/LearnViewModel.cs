using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardMobile.Data;
using FlashcardMobile.Models;
using FlashcardMobile.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlashcardMobile.ViewModels
{
    public partial class LearnViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private readonly SmartQueueService _smartQueueService;
        private Deck? _deck;
        private LearningSession? _currentSession;
        private List<Card> _allCards = new();

        [ObservableProperty] private string _currentCardFront = string.Empty;
        [ObservableProperty] private string _currentCardBack = string.Empty;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] private bool _isBackVisible = false;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEditing))] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] private bool _isDeckFinished = false;
        
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSmartMode))] [NotifyPropertyChangedFor(nameof(IsSequentialMode))] [NotifyPropertyChangedFor(nameof(IsRandomMode))] private LearningOrderMode _orderMode;
        
        [ObservableProperty] private string _editFrontText = string.Empty;
        [ObservableProperty] private string _editBackText = string.Empty;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] [NotifyCanExecuteChangedFor(nameof(CycleLearningModeCommand))] private bool _isEditing = false;
        [ObservableProperty] private bool _showShowBackButton = false;
        [ObservableProperty] private bool _showNextCardButton = false;
        [ObservableProperty] private bool _showReshuffleButton = false;
        [ObservableProperty] private string _reshuffleButtonText = "Neu mischen & Starten";
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] private Card? _currentCard;

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _learnedCount;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _totalCount;
        [ObservableProperty] private string _progressModeLabel = string.Empty;
        [ObservableProperty] private string _deckName = string.Empty;
        [ObservableProperty] private string _backButtonText = "Zurück zum Fach";
        private LearningOrderMode _currentCardFromOrderMode;

        public string ProgressText => OrderMode == LearningOrderMode.Smart ? $"{LearnedCount}% Mastery" : $"{LearnedCount}/{TotalCount}";
        
        public bool IsSmartMode => OrderMode == LearningOrderMode.Smart;
        public bool IsSequentialMode => OrderMode == LearningOrderMode.Sequential;
        public bool IsRandomMode => OrderMode == LearningOrderMode.Random;

        public bool IsCurrentCardSmart => _currentCardFromOrderMode == LearningOrderMode.Smart;
        public bool IsCurrentCardStandard => _currentCardFromOrderMode != LearningOrderMode.Smart;

        public event Action? OnNavigateBack;
        public bool ShowEditButton => IsBackVisible && !IsEditing && CurrentCard != null;

        public LearnViewModel()
        {
            _dbContext = new FlashcardDbContext();
            _smartQueueService = new SmartQueueService();
        }

        public async Task LoadSession(Deck deck, LearningMode mode, List<int>? selectedIds)
        {
            var trackedDeck = _dbContext.Decks.Local.FirstOrDefault(d => d.Id == deck.Id);
            if (trackedDeck != null) _dbContext.Entry(trackedDeck).State = EntityState.Detached;

            _deck = await _dbContext.Decks
                .Include(d => d.Cards)
                .Include(d => d.SubDecks)
                .ThenInclude(sd => sd.Cards)
                .FirstOrDefaultAsync(d => d.Id == deck.Id);

            if (_deck == null) return;

            DeckName = _deck.Name;
            BackButtonText = _deck.ParentDeckId.HasValue ? "Zurück zum Thema" : "Zurück zum Fach";

            // Find or create session
            string selectedIdsJson = selectedIds != null ? JsonSerializer.Serialize(selectedIds.OrderBy(x => x).ToList()) : "[]";
            
            _currentSession = await _dbContext.LearningSessions
                .FirstOrDefaultAsync(s => s.DeckId == _deck.Id && s.Mode == mode && s.SelectedDeckIdsJson == selectedIdsJson);

            if (_currentSession == null)
            {
                _currentSession = new LearningSession
                {
                    DeckId = _deck.Id,
                    Mode = mode,
                    SelectedDeckIdsJson = selectedIdsJson,
                    LastLearnedIndex = 0,
                    LearnedCardIdsJson = "[]",
                    OrderMode = LearningOrderMode.Sequential,
                    LastAccessed = DateTime.Now
                };
                _dbContext.LearningSessions.Add(_currentSession);
            }
            else
            {
                _currentSession.LastAccessed = DateTime.Now;
            }
            await _dbContext.SaveChangesAsync();

            OrderMode = _currentSession.OrderMode;

            // Load cards based on mode
            _allCards.Clear();
            if (mode == LearningMode.MainOnly)
            {
                _allCards.AddRange(_deck.Cards);
            }
            else if (mode == LearningMode.AllRecursive)
            {
                _allCards.AddRange(GetAllCards(_deck));
            }
            else if (mode == LearningMode.CustomSelection && selectedIds != null)
            {
                // Add main deck if selected
                if (selectedIds.Contains(_deck.Id))
                {
                    _allCards.AddRange(_deck.Cards);
                }
                
                // Add subdecks
                // Note: This only works for 1 level deep as loaded. For deeper, we need recursive loading logic in DB query or here.
                // Assuming 1 level for now as per previous implementation.
                foreach (var subDeck in _deck.SubDecks)
                {
                    if (selectedIds.Contains(subDeck.Id))
                    {
                        _allCards.AddRange(GetAllCards(subDeck));
                    }
                }
            }

            UpdateProgressState();
            ShowCardAtCurrentProgress();
        }

        private List<Card> GetAllCards(Deck deck)
        {
            var cards = deck.Cards.ToList();
            foreach (var subDeck in deck.SubDecks)
            {
                cards.AddRange(GetAllCards(subDeck));
            }
            return cards;
        }

        private void UpdateProgressState()
        {
            if (_currentSession == null) return;
            
            switch (OrderMode)
            {
                case LearningOrderMode.Sequential:
                    TotalCount = _allCards.Count;
                    ProgressModeLabel = "Sortiert";
                    LearnedCount = _currentSession.LastLearnedIndex;
                    break;
                case LearningOrderMode.Random:
                    TotalCount = _allCards.Count;
                    ProgressModeLabel = "Zufall";
                    var learnedIds = string.IsNullOrEmpty(_currentSession.LearnedCardIdsJson) 
                        ? new List<int>() 
                        : (JsonSerializer.Deserialize<List<int>>(_currentSession.LearnedCardIdsJson) ?? new List<int>());
                    LearnedCount = learnedIds.Count;
                    break;
                case LearningOrderMode.Smart:
                    TotalCount = 100;
                    ProgressModeLabel = "Smart";
                    // Calculate mastery percentage
                    // We need to fetch scores for all cards in _allCards
                    var cardIds = _allCards.Select(c => c.Id).ToList();
                    var scores = _dbContext.CardSmartScores.Where(s => cardIds.Contains(s.CardId)).ToList();
                    
                    if (!_allCards.Any())
                    {
                        LearnedCount = 0;
                    }
                    else
                    {
                        double totalBoxIndex = scores.Sum(s => s.BoxIndex);
                        // Max possible score is 5 * count
                        double maxScore = _allCards.Count * 5;
                        LearnedCount = (int)((totalBoxIndex / maxScore) * 100);
                    }
                    break;
            }
        }

        private void ShowCardAtCurrentProgress()
        {
            if (_currentSession == null || !_allCards.Any())
            {
                SetFinishedState("Keine Karten in diesem Fach.");
                return;
            }

            UpdateProgressState();

            _currentCardFromOrderMode = OrderMode;
            OnPropertyChanged(nameof(IsCurrentCardSmart));
            OnPropertyChanged(nameof(IsCurrentCardStandard));

            IsEditing = false;
            IsBackVisible = false;
            Card? cardToShow = null;

            if (OrderMode == LearningOrderMode.Sequential)
            {
                var sortedCards = _allCards.OrderBy(c => c.Id).ToList();
                if (_currentSession.LastLearnedIndex < sortedCards.Count)
                {
                    cardToShow = sortedCards[_currentSession.LastLearnedIndex];
                }
                else
                {
                    SetFinishedState("Deck im Sortierten-Modus beendet!");
                    return;
                }
            }
            else if (OrderMode == LearningOrderMode.Random)
            {
                var learnedIds = string.IsNullOrEmpty(_currentSession.LearnedCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_currentSession.LearnedCardIdsJson) ?? new List<int>());
                
                var availableCards = _allCards.Where(c => !learnedIds.Contains(c.Id)).ToList();

                if (!availableCards.Any())
                {
                    SetFinishedState("Deck im Shuffle-Modus beendet!");
                    return;
                }
                cardToShow = availableCards[Random.Shared.Next(availableCards.Count)];
            }
            else if (OrderMode == LearningOrderMode.Smart)
            {
                var cardIds = _allCards.Select(c => c.Id).ToList();
                var scores = _dbContext.CardSmartScores.Where(s => cardIds.Contains(s.CardId)).ToList();
                cardToShow = _smartQueueService.GetNextCard(_allCards, scores);
                
                if (cardToShow == null)
                {
                     // Should not happen if there are cards, but just in case
                     SetFinishedState("Keine Karten verfügbar.");
                     return;
                }
            }
            
            DisplayCard(cardToShow);
        }

        private async Task AdvanceAndShowNextCard()
        {
            if (_currentSession == null) return;
            IsBackVisible = false;

            if (_currentCardFromOrderMode == LearningOrderMode.Sequential)
            {
                _currentSession.LastLearnedIndex++;
            }
            else if (_currentCardFromOrderMode == LearningOrderMode.Random)
            {
                var learnedIds = string.IsNullOrEmpty(_currentSession.LearnedCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_currentSession.LearnedCardIdsJson) ?? new List<int>());
                if (CurrentCard != null && !learnedIds.Contains(CurrentCard.Id))
                {
                    learnedIds.Add(CurrentCard.Id);
                    _currentSession.LearnedCardIdsJson = JsonSerializer.Serialize(learnedIds);
                }
            }
            // Smart mode doesn't advance index, it updates scores via RateCardCommand
            
            await _dbContext.SaveChangesAsync();

            ShowCardAtCurrentProgress();
        }
        
        private void DisplayCard(Card? card)
        {
            if (card != null)
            {
                CurrentCard = card;
                CurrentCardFront = card.Front;
                CurrentCardBack = card.Back;
                IsDeckFinished = false;
                ShowShowBackButton = true;
                ShowNextCardButton = false;
                ShowReshuffleButton = false;
            }
        }
        
        private void SetFinishedState(string message)
        {
            CurrentCardFront = message;
            if (OrderMode == LearningOrderMode.Random)
            {
                CurrentCardBack = "Alle Karten gelernt. Nochmal mischen?";
                ReshuffleButtonText = "Neu mischen & Starten";
            }
            else if (OrderMode == LearningOrderMode.Sequential)
            {
                CurrentCardBack = "Alle Karten gelernt. Nochmal von vorn anfangen?";
                ReshuffleButtonText = "Deck von vorne starten";
            }
            else
            {
                CurrentCardBack = "Smart Learning Session beendet (sollte nicht passieren).";
                ReshuffleButtonText = "Weiter lernen";
            }
            CurrentCard = null;
            IsBackVisible = true;
            IsDeckFinished = true;
            ShowShowBackButton = false;
            ShowNextCardButton = false;
            ShowReshuffleButton = true;
        }

        [RelayCommand(CanExecute = nameof(CanCycleLearningMode))]
        private async Task CycleLearningMode()
        {
            if (_currentSession == null) return;

            // Determine next mode
            LearningOrderMode nextMode = OrderMode;
            switch (OrderMode)
            {
                case LearningOrderMode.Sequential:
                    nextMode = LearningOrderMode.Random;
                    break;
                case LearningOrderMode.Random:
                    nextMode = LearningOrderMode.Smart;
                    break;
                case LearningOrderMode.Smart:
                    nextMode = LearningOrderMode.Sequential;
                    break;
            }

            OrderMode = nextMode;
            _currentSession.OrderMode = OrderMode;
            await _dbContext.SaveChangesAsync();

            // If we are currently viewing a card back
            if (CurrentCard != null && IsBackVisible)
            {
                // If the CURRENT card was Smart, we MUST wait for rating.
                // Do NOT advance. The UI will show the new mode icon (OrderMode updated),
                // but the buttons will remain Rating buttons (IsCurrentCardSmart is still true).
                if (_currentCardFromOrderMode == LearningOrderMode.Smart)
                {
                    return;
                }
                
                // If the current card was NOT Smart (Sequential or Random),
                // we should immediately advance to the next card in the NEW mode.
                await NextCard();
            }
            else if (CurrentCard == null)
            {
                // If no card is currently shown (e.g. finished state), refresh to see if new mode has cards.
                ShowCardAtCurrentProgress();
            }
        }

        private bool CanCycleLearningMode() => !IsEditing;

        [RelayCommand]
        private async Task RateCard(string ratingStr)
        {
            if (CurrentCard == null || _currentSession == null || !int.TryParse(ratingStr, out int rating)) return;

            // Find or create score
            var score = await _dbContext.CardSmartScores.FirstOrDefaultAsync(s => s.CardId == CurrentCard.Id);
            if (score == null)
            {
                score = new CardSmartScore { CardId = CurrentCard.Id, Score = 0, BoxIndex = 0, LastReviewed = DateTime.MinValue };
                _dbContext.CardSmartScores.Add(score);
            }

            _smartQueueService.CalculateNewScore(score, rating);
            await _dbContext.SaveChangesAsync();

            await AdvanceAndShowNextCard();
        }

        [RelayCommand]
        private async Task ResetDeckProgress()
        {
            if (_currentSession == null) return;

            if (OrderMode == LearningOrderMode.Random)
            {
                _currentSession.LearnedCardIdsJson = "[]";
            }
            else if (OrderMode == LearningOrderMode.Sequential)
            {
                _currentSession.LastLearnedIndex = 0;
            }
            else if (OrderMode == LearningOrderMode.Smart)
            {
                // Reset scores for all cards in this deck/session
                var cardIds = _allCards.Select(c => c.Id).ToList();
                var scores = await _dbContext.CardSmartScores.Where(s => cardIds.Contains(s.CardId)).ToListAsync();
                
                foreach (var score in scores)
                {
                    score.Score = 0;
                    score.BoxIndex = 0;
                    score.LastReviewed = DateTime.MinValue;
                }
            }
            
            await _dbContext.SaveChangesAsync();
            ShowCardAtCurrentProgress();
        }

        [RelayCommand]
        private void ShowBack()
        {
            IsBackVisible = true;
            ShowShowBackButton = false;
            ShowNextCardButton = true;
        }

        [RelayCommand]
        private async Task NextCard()
        {
            if (IsDeckFinished)
            {
                await ResetDeckProgress();
            }
            else
            {
                await AdvanceAndShowNextCard();
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }

        private bool CanAdvance() => !IsEditing;

        [RelayCommand(CanExecute = nameof(CanAdvance))]
        private async Task Advance()
        {
            if (!IsBackVisible)
            {
                ShowBack();
            }
            else
            {
                // In Smart Mode, we must rate the card. "Enter" (which triggers Advance) should not skip rating.
                if (IsCurrentCardSmart)
                {
                    return;
                }
                await NextCard();
            }
        }
        
        [RelayCommand]
        private void StartEdit()
        {
            if (CurrentCard == null) return;
            IsEditing = true;
            EditFrontText = CurrentCardFront;
            EditBackText = CurrentCardBack;
            ShowShowBackButton = false;
            ShowNextCardButton = false;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            if (IsDeckFinished)
            {
                ShowReshuffleButton = true;
            }
            else
            {
                ShowNextCardButton = true;
            }
        }

        [RelayCommand]
        private async Task SaveEdit()
        {
            if (CurrentCard == null) { CancelEdit(); return; }
            var trackedCard = await _dbContext.Cards.FindAsync(CurrentCard.Id);
            if (trackedCard != null)
            {
                trackedCard.Front = EditFrontText;
                trackedCard.Back = EditBackText;
                CurrentCardFront = EditFrontText;
                CurrentCardBack = EditBackText;
                CurrentCard.Front = EditFrontText;
                CurrentCard.Back = EditBackText;
                await _dbContext.SaveChangesAsync();
            }
            CancelEdit();
        }
    }
}
