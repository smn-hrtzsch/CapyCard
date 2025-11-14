using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class DeckDetailViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private Deck? _currentDeck;
        private Card? _cardToEdit;

        [ObservableProperty]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        private string _newCardBack = string.Empty;

        [ObservableProperty]
        private string _deckName = "Fach laden...";

        [ObservableProperty]
        private string _cardCountText = "Karten anzeigen (0)";
        
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoToCardListCommand))] 
        [NotifyCanExecuteChangedFor(nameof(GoToLearnCommand))]    
        private bool _hasCards = false;

        [ObservableProperty]
        private string _saveButtonText = "Karte hinzufügen";

        [ObservableProperty]
        private bool _isEditing = false;


        public ObservableCollection<Card> Cards { get; } = new();

        public event Action? OnNavigateBack;
        public event Action<Deck>? OnNavigateToCardList; 
        public event Action<List<Card>>? OnNavigateToLearn;
        public event Action<Deck, int>? OnCardCountUpdated;
        public event Action? RequestFrontFocus;

        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async Task LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = deck.Name;
            ResetToAddingMode(); 
            await RefreshCardDataAsync();
        }
        
        public async Task LoadCardForEditing(Deck deck, Card card)
        {
            _currentDeck = deck;
            _cardToEdit = card;
            DeckName = deck.Name;

            NewCardFront = card.Front;
            NewCardBack = card.Back;
            
            SaveButtonText = "Änderungen speichern";
            IsEditing = true;
            
            await RefreshCardDataAsync();
        }

        public async Task RefreshCardDataAsync()
        {
            if (_currentDeck == null) return;
            
            // --- HIER IST DIE KORREKTUR ---
            // 'AsNoTracking()' zwingt EF Core, die Datenbank-Datei
            // neu abzufragen (z.B. nach einem Löschvorgang im CardListViewModel)
            // und nicht die veralteten Daten aus seinem eigenen Cache zu verwenden.
            var deckFromDb = await _dbContext.Decks
                                 .AsNoTracking() // <-- HINZUGEFÜGT
                                 .Include(d => d.Cards)
                                 .FirstOrDefaultAsync(d => d.Id == _currentDeck.Id);
            
            // Wir müssen _currentDeck aktualisieren, falls es null war, 
            // aber hauptsächlich brauchen wir die Kartenliste.
            if (deckFromDb == null) 
            {
                // Das Deck selbst wurde gelöscht, gehe zurück
                GoBack();
                return; 
            }
            
            _currentDeck = deckFromDb; 

            Cards.Clear();
            foreach (var card in deckFromDb.Cards.OrderBy(c => c.Id))
            {
                Cards.Add(card);
            }
            
            UpdateCardCount(Cards.Count);
        }
        
        [RelayCommand]
        private async Task SaveCard()
        {
            if (_currentDeck == null || string.IsNullOrWhiteSpace(NewCardFront) || string.IsNullOrWhiteSpace(NewCardBack))
            {
                return;
            }

            if (_cardToEdit != null)
            {
                // FALL 1: WIR BEARBEITEN
                var trackedCard = _dbContext.Cards.Find(_cardToEdit.Id);
                if (trackedCard != null)
                {
                    trackedCard.Front = NewCardFront;
                    trackedCard.Back = NewCardBack;
                }
                else
                {
                    _cardToEdit.Front = NewCardFront; 
                    _cardToEdit.Back = NewCardBack;
                    _dbContext.Entry(_cardToEdit).State = EntityState.Modified;
                }
                await _dbContext.SaveChangesAsync();
                
                OnNavigateToCardList?.Invoke(_currentDeck);
                ResetToAddingMode();
            }
            else
            {
                // FALL 2: WIR FÜGEN HINZU
                var newCard = new Card
                {
                    Front = NewCardFront,
                    Back = NewCardBack,
                    DeckId = _currentDeck.Id
                };
                _dbContext.Cards.Add(newCard);
                await _dbContext.SaveChangesAsync();
                await RefreshCardDataAsync(); 
                RequestFrontFocus?.Invoke();
            }

            if(_cardToEdit == null) 
            {
                NewCardFront = string.Empty;
                NewCardBack = string.Empty;
            }
        }
        
        [RelayCommand]
        private void CancelEdit()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
            ResetToAddingMode();
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
        
        [RelayCommand(CanExecute = nameof(HasCards))]
        private void GoToCardList()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
        }
        
        [RelayCommand(CanExecute = nameof(HasCards))]
        private void GoToLearn()
        {
            if (_currentDeck != null)
            {
                OnNavigateToLearn?.Invoke(Cards.ToList());
            }
        }
        
        private void UpdateCardCount(int count)
        {
            CardCountText = $"Karteikarten anzeigen ({count})";
            HasCards = count > 0;

            if (_currentDeck != null)
            {
                OnCardCountUpdated?.Invoke(_currentDeck, count);
            }
        }

        private void ResetToAddingMode()
        {
            _cardToEdit = null;
            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
            SaveButtonText = "Karte hinzufügen";
            IsEditing = false;
        }
    }
}