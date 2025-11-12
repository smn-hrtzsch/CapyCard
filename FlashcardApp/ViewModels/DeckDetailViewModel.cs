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

        [ObservableProperty]
        private string _cardCountText = "Karten anzeigen (0)";

        // HINWEIS: Diese Liste wird jetzt nur noch zum Hinzufügen und
        // für die Zählung beim Laden verwendet. Die CardListView lädt ihre eigene Liste.
        public ObservableCollection<Card> Cards { get; } = new();

        public event Action? OnNavigateBack;
        
        // NEU: Signatur geändert. Übergibt nur noch das Deck.
        public event Action<Deck>? OnNavigateToCardList;

        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        // 'async' und 'Task' hinzugefügt, damit wir darauf warten können
        public async Task LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = deck.Name;
            
            // Lade die Karten neu, um die Zählung zu aktualisieren
            await RefreshCardCountAsync();
        }

        // NEU: Öffentliche Methode, die vom MainViewModel aufgerufen werden kann
        public async Task RefreshCardCountAsync()
        {
            if (_currentDeck == null) return;

            // Zähle die Karten direkt in der DB
            var count = await _dbContext.Cards
                            .CountAsync(c => c.DeckId == _currentDeck.Id);
            
            UpdateCardCount(count);
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
            
            // NEU: Zähler über die DB-Methode aktualisieren
            await RefreshCardCountAsync();

            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
        
        [RelayCommand]
        private void GoToCardList()
        {
            if (_currentDeck != null)
            {
                // NEU: Übergibt nur noch das Deck, nicht mehr die Kartenliste
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
        }
        
        private void UpdateCardCount(int count)
        {
            CardCountText = $"Karteikarten anzeigen ({count})";
        }
    }
}