using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
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

        // NEU: Text für den Navigations-Button
        [ObservableProperty]
        private string _cardCountText = "Karten anzeigen (0)";

        // HINWEIS: Die 'Cards'-Liste bleibt hier. 
        // Sie wird im Hintergrund geladen und an die CardListView übergeben.
        public ObservableCollection<Card> Cards { get; } = new();

        // Navigation zurück zur Fächer-Liste
        public event Action? OnNavigateBack;
        
        // NEU: Navigation zur Karten-Liste
        public event Action<Deck, ObservableCollection<Card>>? OnNavigateToCardList;


        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async void LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = deck.Name;
            
            Cards.Clear();
            var cardsFromDb = await _dbContext.Cards
                                .Where(c => c.DeckId == _currentDeck.Id)
                                .ToListAsync();
            
            foreach (var card in cardsFromDb)
            {
                Cards.Add(card);
            }
            
            // NEU: Zähler aktualisieren
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

            Cards.Add(newCard);
            
            // NEU: Zähler aktualisieren
            UpdateCardCount(Cards.Count);

            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
        
        // NEU: Befehl, um zur Karten-Liste zu navigieren
        [RelayCommand]
        private void GoToCardList()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck, Cards);
            }
        }
        
        // NEU: Hilfsmethode für den Button-Text
        private void UpdateCardCount(int count)
        {
            CardCountText = $"Karteikarten anzeigen ({count})";
        }
    }
}