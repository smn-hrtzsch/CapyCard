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
        private Deck? _currentDeck; // Das aktuell ausgewählte Fach

        [ObservableProperty]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        private string _newCardBack = string.Empty;

        // Zeigt den Namen des Fachs oben an
        [ObservableProperty]
        private string _deckName = "Fach laden...";

        public ObservableCollection<Card> Cards { get; } = new();

        // Event, um dem MainViewModel zu sagen: "Wir wollen zurück!"
        public event Action? OnNavigateBack;

        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        // Wird vom MainViewModel aufgerufen, um das Fach zu laden
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

            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
        }

        // Command für den "Zurück"-Button
        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
    }
}