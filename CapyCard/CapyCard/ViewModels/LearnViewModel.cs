using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    public partial class LearnViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private readonly SmartQueueService _smartQueueService;
        private LearningSession? _currentSession;
        private List<Card> _allCards = new();
        private readonly Dictionary<int, CardSmartScore> _smartScoresByCardId = new();
        private bool _smartScoresLoaded;
        private int _loadSequence;

        [ObservableProperty] private string _currentCardFront = string.Empty;
        [ObservableProperty] private string _currentCardBack = string.Empty;
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(ShowEditButton))] 
        [NotifyCanExecuteChangedFor(nameof(RateCardCommand))]
        [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))]
        private bool _isBackVisible = false;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEditing))] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] private bool _isDeckFinished = false;
        
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSmartMode))] [NotifyPropertyChangedFor(nameof(IsSequentialMode))] [NotifyPropertyChangedFor(nameof(IsRandomMode))] [NotifyPropertyChangedFor(nameof(ProgressText))] private LearningOrderMode _strategy;
        
        [ObservableProperty] private string _editFrontText = string.Empty;
        [ObservableProperty] private string _editBackText = string.Empty;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] [NotifyCanExecuteChangedFor(nameof(CycleLearningModeCommand))] private bool _isEditing = false;
        [ObservableProperty] private bool _showShowBackButton = false;
        [ObservableProperty] private bool _showNextCardButton = false;
        [ObservableProperty] private bool _showReshuffleButton = false;
        [ObservableProperty] private string _reshuffleButtonText = "Neu mischen & Starten";
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] private Card? _currentCard;

        [ObservableProperty] private bool _isImagePreviewOpen = false;
        [ObservableProperty] private object? _previewImageSource;
        private double _originalImageWidth;
        private double _originalImageHeight;
        private double _imageZoomLevel = 1.0;

        public double ImageZoomLevel
        {
            get => _imageZoomLevel;
            set
            {
                if (SetProperty(ref _imageZoomLevel, Math.Clamp(value, 0.1, 5.0)))
                {
                    OnPropertyChanged(nameof(ScaledImageWidth));
                    OnPropertyChanged(nameof(ScaledImageHeight));
                }
            }
        }

        public double ScaledImageWidth => _originalImageWidth * _imageZoomLevel;
        public double ScaledImageHeight => _originalImageHeight * _imageZoomLevel;

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _learnedCount;
        [ObservableProperty] private double _defaultZoomLevel = 1.0;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _totalCount;
        [ObservableProperty] private string _progressModeLabel = string.Empty;
        [ObservableProperty] private string _deckName = string.Empty;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _backButtonText = "Zurück zum Fach";
        private LearningOrderMode _currentCardFromStrategy;

        public string ProgressText => Strategy == LearningOrderMode.Smart ? $"{LearnedCount}% Mastery" : $"{LearnedCount}/{TotalCount}";
        
        public bool IsSmartMode => Strategy == LearningOrderMode.Smart;
        public bool IsSequentialMode => Strategy == LearningOrderMode.Sequential;
        public bool IsRandomMode => Strategy == LearningOrderMode.Random;

        public bool IsCurrentCardSmart => _currentCardFromStrategy == LearningOrderMode.Smart;
        public bool IsCurrentCardStandard => _currentCardFromStrategy != LearningOrderMode.Smart;

        public event Action? OnNavigateBack;
        public bool ShowEditButton => IsBackVisible && !IsEditing && CurrentCard != null;

        public LearnViewModel()
        {
            _dbContext = new FlashcardDbContext();
            _smartQueueService = new SmartQueueService();
        }

        [RelayCommand]
        private void OpenImagePreview(object imageSource)
        {
            if (imageSource is Bitmap bitmap)
            {
                _originalImageWidth = bitmap.Size.Width;
                _originalImageHeight = bitmap.Size.Height;
                OnPropertyChanged(nameof(ScaledImageWidth));
                OnPropertyChanged(nameof(ScaledImageHeight));
            }
            PreviewImageSource = imageSource;
            // Zoom calculation is done in View (Code Behind) to match window size
            IsImagePreviewOpen = true;
        }

        [RelayCommand]
        private void CloseImagePreview()
        {
            IsImagePreviewOpen = false;
            PreviewImageSource = null;
            _originalImageWidth = 0;
            _originalImageHeight = 0;
            OnPropertyChanged(nameof(ScaledImageWidth));
            OnPropertyChanged(nameof(ScaledImageHeight));
        }
        
        [RelayCommand]
        private void ZoomIn()
        {
            ImageZoomLevel += 0.05;
        }

        [RelayCommand]
        private void ZoomOut()
        {
            ImageZoomLevel -= 0.05;
        }

        [RelayCommand]
        private void ResetZoom()
        {
             ImageZoomLevel = DefaultZoomLevel;
        }

        public async Task LoadSessionAsync(Deck deck, LearningMode scope, List<int>? selectedIds)
        {
            int loadId = ++_loadSequence;
            IsLoading = true;

            try
            {
                _smartScoresLoaded = false;
                _smartScoresByCardId.Clear();
                _allCards.Clear();
                CurrentCard = null;
                CurrentCardFront = string.Empty;
                CurrentCardBack = string.Empty;
                IsDeckFinished = false;
                IsBackVisible = false;
                ShowShowBackButton = false;
                ShowNextCardButton = false;
                ShowReshuffleButton = false;

                var deckInfo = await _dbContext.Decks
                    .AsNoTracking()
                    .Where(d => d.Id == deck.Id)
                    .Select(d => new { d.Id, d.Name, d.ParentDeckId })
                    .FirstOrDefaultAsync();

                if (deckInfo == null)
                {
                    return;
                }

                if (loadId != _loadSequence)
                {
                    return;
                }

                DeckName = deckInfo.Name;
                BackButtonText = deckInfo.ParentDeckId.HasValue ? "Zurück zum Thema" : "Zurück zum Fach";

                // Find or create session
                string selectedIdsJson = selectedIds != null ? JsonSerializer.Serialize(selectedIds.OrderBy(x => x).ToList()) : "[]";

                _currentSession = await _dbContext.LearningSessions
                    .FirstOrDefaultAsync(s => s.DeckId == deckInfo.Id && s.Scope == scope && s.SelectedDeckIdsJson == selectedIdsJson);

                if (_currentSession == null)
                {
                    _currentSession = new LearningSession
                    {
                        DeckId = deckInfo.Id,
                        Scope = scope,
                        SelectedDeckIdsJson = selectedIdsJson,
                        LastLearnedIndex = 0,
                        LearnedCardIdsJson = "[]",
                        Strategy = LearningOrderMode.Sequential,
                        LastAccessed = DateTime.Now
                    };
                    _dbContext.LearningSessions.Add(_currentSession);
                }
                else
                {
                    _currentSession.LastAccessed = DateTime.Now;
                }
                await _dbContext.SaveChangesAsync();

                Strategy = _currentSession.Strategy;

                var deckIds = await ResolveDeckIdsAsync(deckInfo.Id, scope, selectedIds);
                if (loadId != _loadSequence)
                {
                    return;
                }

                if (deckIds.Count > 0)
                {
                    _allCards = await _dbContext.Cards
                        .AsNoTracking()
                        .Where(c => deckIds.Contains(c.DeckId))
                        .ToListAsync();
                }
                else
                {
                    _allCards.Clear();
                }

                if (loadId != _loadSequence)
                {
                    return;
                }

                if (Strategy == LearningOrderMode.Smart)
                {
                    await EnsureSmartScoresLoadedAsync(loadId);
                }

                UpdateProgressState();
                ShowCardAtCurrentProgress();
            }
            finally
            {
                if (loadId == _loadSequence)
                {
                    IsLoading = false;
                }
            }
        }

        public Task LoadSession(Deck deck, LearningMode scope, List<int>? selectedIds) =>
            LoadSessionAsync(deck, scope, selectedIds);

        private async Task<List<int>> ResolveDeckIdsAsync(int deckId, LearningMode scope, List<int>? selectedIds)
        {
            if (scope == LearningMode.MainOnly)
            {
                return new List<int> { deckId };
            }

            if (scope == LearningMode.CustomSelection && (selectedIds == null || selectedIds.Count == 0))
            {
                return new List<int>();
            }

            var deckRelations = await _dbContext.Decks
                .AsNoTracking()
                .Select(d => new { d.Id, d.ParentDeckId })
                .ToListAsync();

            var childrenByParent = deckRelations
                .Where(d => d.ParentDeckId.HasValue)
                .GroupBy(d => d.ParentDeckId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            var result = new HashSet<int>();

            void AddWithDescendants(int id)
            {
                if (!result.Add(id)) return;
                if (childrenByParent.TryGetValue(id, out var children))
                {
                    foreach (var childId in children)
                    {
                        AddWithDescendants(childId);
                    }
                }
            }

            if (scope == LearningMode.AllRecursive)
            {
                AddWithDescendants(deckId);
            }
            else if (scope == LearningMode.CustomSelection && selectedIds != null)
            {
                foreach (var selectedId in selectedIds)
                {
                    AddWithDescendants(selectedId);
                }
            }

            return result.ToList();
        }

        private async Task EnsureSmartScoresLoadedAsync(int? loadId = null)
        {
            if (_smartScoresLoaded)
            {
                return;
            }

            var cardIds = _allCards.Select(c => c.Id).ToList();
            if (cardIds.Count == 0)
            {
                _smartScoresByCardId.Clear();
                _smartScoresLoaded = true;
                return;
            }

            var scores = await _dbContext.CardSmartScores
                .AsNoTracking()
                .Where(s => cardIds.Contains(s.CardId))
                .ToListAsync();

            if (loadId != null && loadId != _loadSequence)
            {
                return;
            }

            _smartScoresByCardId.Clear();
            foreach (var score in scores)
            {
                _smartScoresByCardId[score.CardId] = score;
            }
            _smartScoresLoaded = true;
        }

        private void UpdateProgressState()
        {
            if (_currentSession == null) return;
            
            switch (Strategy)
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
                    if (!_allCards.Any())
                    {
                        LearnedCount = 0;
                    }
                    else
                    {
                        double totalBoxIndex = 0;
                        foreach (var card in _allCards)
                        {
                            if (_smartScoresByCardId.TryGetValue(card.Id, out var score))
                            {
                                totalBoxIndex += score.BoxIndex;
                            }
                        }

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

            _currentCardFromStrategy = Strategy;
            OnPropertyChanged(nameof(IsCurrentCardSmart));
            OnPropertyChanged(nameof(IsCurrentCardStandard));

            IsEditing = false;
            IsBackVisible = false;
            Card? cardToShow = null;

            if (Strategy == LearningOrderMode.Sequential)
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
            else if (Strategy == LearningOrderMode.Random)
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
            else if (Strategy == LearningOrderMode.Smart)
            {
                cardToShow = _smartQueueService.GetNextCard(_allCards, _smartScoresByCardId);
                
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

            if (_currentCardFromStrategy == LearningOrderMode.Sequential)
            {
                _currentSession.LastLearnedIndex++;
            }
            else if (_currentCardFromStrategy == LearningOrderMode.Random)
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
            if (Strategy == LearningOrderMode.Random)
            {
                CurrentCardBack = "Alle Karten gelernt. Nochmal mischen?";
                ReshuffleButtonText = "Neu mischen & Starten";
            }
            else if (Strategy == LearningOrderMode.Sequential)
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
            LearningOrderMode nextMode = Strategy;
            switch (Strategy)
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

            Strategy = nextMode;
            _currentSession.Strategy = Strategy;
            await _dbContext.SaveChangesAsync();

            if (Strategy == LearningOrderMode.Smart)
            {
                await EnsureSmartScoresLoadedAsync();
            }

            // If we are currently viewing a card back
            if (CurrentCard != null && IsBackVisible)
            {
                // If the CURRENT card was Smart, we MUST wait for rating.
                // Do NOT advance. The UI will show the new mode icon (OrderMode updated),
                // but the buttons will remain Rating buttons (IsCurrentCardSmart is still true).
                if (_currentCardFromStrategy == LearningOrderMode.Smart)
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

        [RelayCommand(CanExecute = nameof(CanRateCard))]
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

            _smartScoresByCardId[score.CardId] = score;
            _smartScoresLoaded = true;

            await AdvanceAndShowNextCard();
        }

        private bool CanRateCard(string ratingStr)
        {
            // Only allow rating if the back is visible (card has been revealed)
            // and we are in Smart Mode (where rating buttons are used).
            return IsBackVisible && IsCurrentCardSmart;
        }

        [RelayCommand]
        private async Task ResetDeckProgress()
        {
            if (_currentSession == null) return;

            if (Strategy == LearningOrderMode.Random)
            {
                _currentSession.LearnedCardIdsJson = "[]";
            }
            else if (Strategy == LearningOrderMode.Sequential)
            {
                _currentSession.LastLearnedIndex = 0;
            }
            else if (Strategy == LearningOrderMode.Smart)
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

                _smartScoresByCardId.Clear();
                foreach (var score in scores)
                {
                    _smartScoresByCardId[score.CardId] = score;
                }
                _smartScoresLoaded = true;
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
