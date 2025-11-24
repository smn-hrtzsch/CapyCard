using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardMobile.Data;
using FlashcardMobile.Models;
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
        private Deck? _deck;
        private LearningSession? _currentSession;
        private List<Card> _allCards = new();

        [ObservableProperty] private string _currentCardFront = string.Empty;
        [ObservableProperty] private string _currentCardBack = string.Empty;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] private bool _isBackVisible = false;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEditing))] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] private bool _isDeckFinished = false;
        [ObservableProperty] private bool _isRandomOrder;
        [ObservableProperty] private string _editFrontText = string.Empty;
        [ObservableProperty] private string _editBackText = string.Empty;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] [NotifyCanExecuteChangedFor(nameof(AdvanceCommand))] [NotifyCanExecuteChangedFor(nameof(ToggleRandomOrderCommand))] private bool _isEditing = false;
        [ObservableProperty] private bool _showShowBackButton = false;
        [ObservableProperty] private bool _showNextCardButton = false;
        [ObservableProperty] private bool _showReshuffleButton = false;
        [ObservableProperty] private string _reshuffleButtonText = "Neu mischen & Starten";
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEditButton))] private Card? _currentCard;

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _learnedCount;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressText))] private int _totalCount;
        [ObservableProperty] private string _progressModeLabel = string.Empty;
        [ObservableProperty] private string _deckName = string.Empty;
        private bool _isCurrentCardFromRandomOrder;

        public string ProgressText => $"{LearnedCount}/{TotalCount}";

        public event Action? OnNavigateBack;
        public bool ShowEditButton => IsBackVisible && !IsEditing && CurrentCard != null;

        public LearnViewModel()
        {
            _dbContext = new FlashcardDbContext();
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
                    IsRandomOrder = false,
                    LastAccessed = DateTime.Now
                };
                _dbContext.LearningSessions.Add(_currentSession);
            }
            else
            {
                _currentSession.LastAccessed = DateTime.Now;
            }
            await _dbContext.SaveChangesAsync();

            IsRandomOrder = _currentSession.IsRandomOrder;

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
            TotalCount = _allCards.Count;

            if (!IsRandomOrder)
            {
                ProgressModeLabel = "Sortiert";
                LearnedCount = _currentSession.LastLearnedIndex;
            }
            else
            {
                ProgressModeLabel = "Zufall";
                var learnedIds = string.IsNullOrEmpty(_currentSession.LearnedCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_currentSession.LearnedCardIdsJson) ?? new List<int>());
                LearnedCount = learnedIds.Count;
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

            _isCurrentCardFromRandomOrder = IsRandomOrder;
            IsEditing = false;
            IsBackVisible = false;
            Card? cardToShow = null;

            if (!IsRandomOrder)
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
            else
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
            
            DisplayCard(cardToShow);
        }

        private async Task AdvanceAndShowNextCard()
        {
            if (_currentSession == null) return;
            IsBackVisible = false;

            if (!_isCurrentCardFromRandomOrder)
            {
                _currentSession.LastLearnedIndex++;
            }
            else
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
            if (IsRandomOrder)
            {
                CurrentCardBack = "Alle Karten gelernt. Nochmal mischen?";
                ReshuffleButtonText = "Neu mischen & Starten";
            }
            else
            {
                CurrentCardBack = "Alle Karten gelernt. Nochmal von vorn anfangen?";
                ReshuffleButtonText = "Deck von vorne starten";
            }
            CurrentCard = null;
            IsBackVisible = true;
            IsDeckFinished = true;
            ShowShowBackButton = false;
            ShowNextCardButton = false;
            ShowReshuffleButton = true;
        }

        [RelayCommand(CanExecute = nameof(CanToggleRandomOrder))]
        private async Task ToggleRandomOrder()
        {
            if (_currentSession == null) return;

            _currentSession.IsRandomOrder = IsRandomOrder;
            await _dbContext.SaveChangesAsync();
            
            if (CurrentCard == null || IsBackVisible)
            {
                ShowCardAtCurrentProgress();
            }
        }

        private bool CanToggleRandomOrder() => !IsEditing;

        [RelayCommand]
        private async Task ResetDeckProgress()
        {
            if (_currentSession == null) return;

            if (IsRandomOrder)
            {
                _currentSession.LearnedCardIdsJson = "[]";
            }
            else
            {
                _currentSession.LastLearnedIndex = 0;
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
