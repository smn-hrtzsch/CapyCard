using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; // NEU
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class DeckDetailViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private Deck? _currentDeck; 

        [ObservableProperty]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        private string _newCardBack = string.Empty;

        [ObservableProperty]
        private string _deckName = "Fach laden...";

        [ObservableProperty]
        private string _cardCountText = "Karten anzeigen (0)";
        
        // NEU: Steuert die IsEnabled-Eigenschaft der Buttons
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoToCardListCommand))] // Aktualisiert den "Anzeigen"-Button
        [NotifyCanExecuteChangedFor(nameof(GoToLearnCommand))]    // Aktualisiert den "Lernen"-Button
        private bool _hasCards = false;

        
        public ObservableCollection<Card> Cards { get; } = new();

        
        public event Action? OnNavigateBack;
        public event Action<Deck>? OnNavigateToCardList;
        
        // NEU: Event für den Lern-Modus. Übergibt eine Kopie der Karten.
        public event Action<List<Card>>? OnNavigateToLearn;


        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async Task LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = deck.Name;
            await RefreshCardDataAsync(); // Lädt Karten und aktualisiert Zähler
        }

        public async Task RefreshCardDataAsync()
        {
            if (_currentDeck == null) return;

            // Lädt die Karten in die lokale Collection
            Cards.Clear();
            var cardsFromDb = await _dbContext.Cards
                                .Where(c => c.DeckId == _currentDeck.Id)
                                .ToListAsync();
            foreach (var card in cardsFromDb)
            {
                Cards.Add(card);
            }
            
            UpdateCardCount(Cards.Count);
        }

        [RelayCommand]
        private async Task AddCard()
        {
            if (_currentDeck == null || string.IsNullOrWhiteSpace(NewCardFront) || string.IsNullOrWhiteSpace(NewCardBack))
            {
                return;
            }
            var newCard = new Card
            {
                Front = NewCardFront,
                Back = NewCardBack,
                DeckId = _currentDeck.Id
            };
            _dbContext.Cards.Add(newCard);
            await _dbContext.SaveChangesAsync();
            
            // NEU: Lädt Karten neu, statt nur Zähler zu aktualisieren
            await RefreshCardDataAsync();

            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
        
        // NEU: CanExecute prüft jetzt die 'HasCards'-Eigenschaft
        [RelayCommand(CanExecute = nameof(HasCards))]
        private void GoToCardList()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
        }
        
        // NEU: Befehl für den "Lernen"-Button
        [RelayCommand(CanExecute = nameof(HasCards))]
        private void GoToLearn()
        {
            if (_currentDeck != null)
            {
                // Wir übergeben eine Kopie der Liste, damit das Original nicht verändert wird
                OnNavigateToLearn?.Invoke(Cards.ToList());
            }
        }
        
        private void UpdateCardCount(int count)
        {
            CardCountText = $"Karteikarten anzeigen ({count})";
            HasCards = count > 0; // NEU: Aktualisiert die Eigenschaft
        }
    }
}