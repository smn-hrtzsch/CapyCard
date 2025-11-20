using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class LearnViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private Deck? _deck;
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
        private bool _isCurrentCardFromRandomOrder;

        public string ProgressText => $"{LearnedCount}/{TotalCount}";

        public event Action? OnNavigateBack;
        public bool ShowEditButton => IsBackVisible && !IsEditing && CurrentCard != null;

        public LearnViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async Task LoadDeck(Deck deck)
        {
            var trackedDeck = _dbContext.Decks.Local.FirstOrDefault(d => d.Id == deck.Id);
            if (trackedDeck != null) _dbContext.Entry(trackedDeck).State = EntityState.Detached;

            _deck = await _dbContext.Decks.Include(d => d.Cards).FirstOrDefaultAsync(d => d.Id == deck.Id);

            if (_deck == null || !_deck.Cards.Any())
            {
                _allCards = new List<Card>();
                SetFinishedState("Keine Karten in diesem Fach.");
                return;
            }

            _allCards = _deck.Cards.ToList();
            IsRandomOrder = _deck.IsRandomOrder;
            
            UpdateProgressState();
            ShowCardAtCurrentProgress();
        }

        private void UpdateProgressState()
        {
            if (_deck == null) return;
            TotalCount = _allCards.Count;

            if (!IsRandomOrder)
            {
                ProgressModeLabel = "Sortiert";
                LearnedCount = _deck.LastLearnedCardIndex;
            }
            else
            {
                ProgressModeLabel = "Zufall";
                var learnedIds = string.IsNullOrEmpty(_deck.LearnedShuffleCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_deck.LearnedShuffleCardIdsJson) ?? new List<int>());
                LearnedCount = learnedIds.Count;
            }
        }

        private void ShowCardAtCurrentProgress()
        {
            if (_deck == null || !_allCards.Any())
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
                if (_deck.LastLearnedCardIndex < sortedCards.Count)
                {
                    cardToShow = sortedCards[_deck.LastLearnedCardIndex];
                }
                else
                {
                    SetFinishedState("Deck im Sortierten-Modus beendet!");
                    return;
                }
            }
            else
            {
                var learnedIds = string.IsNullOrEmpty(_deck.LearnedShuffleCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_deck.LearnedShuffleCardIdsJson) ?? new List<int>());
                
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
            if (_deck == null) return;
            IsBackVisible = false;

            if (!_isCurrentCardFromRandomOrder)
            {
                _deck.LastLearnedCardIndex++;
            }
            else
            {
                var learnedIds = string.IsNullOrEmpty(_deck.LearnedShuffleCardIdsJson) 
                    ? new List<int>() 
                    : (JsonSerializer.Deserialize<List<int>>(_deck.LearnedShuffleCardIdsJson) ?? new List<int>());
                if (CurrentCard != null && !learnedIds.Contains(CurrentCard.Id))
                {
                    learnedIds.Add(CurrentCard.Id);
                    _deck.LearnedShuffleCardIdsJson = JsonSerializer.Serialize(learnedIds);
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
            // The IsRandomOrder property is already updated by the TwoWay binding from the UI.
            // This command just persists the change and updates the view.
            if (_deck == null) return;

            _deck.IsRandomOrder = IsRandomOrder;
            await _dbContext.SaveChangesAsync();
            
            // Only refresh if we are NOT currently viewing a card (e.g. we are at the finish screen)
            // OR if the back is already visible (user wants to skip to next card in new mode).
            // If we are viewing the front of a card, we want to keep it visible and only switch logic for the NEXT card.
            if (CurrentCard == null || IsBackVisible)
            {
                ShowCardAtCurrentProgress();
            }
        }

        private bool CanToggleRandomOrder() => !IsEditing;

        [RelayCommand]
        private async Task ResetDeckProgress()
        {
            if (_deck == null) return;

            if (IsRandomOrder)
            {
                // If we are in random mode, only reset the shuffle progress.
                _deck.LearnedShuffleCardIdsJson = "[]";
            }
            else
            {
                // If we are in sorted mode, only reset the sorted progress.
                _deck.LastLearnedCardIndex = 0;
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
