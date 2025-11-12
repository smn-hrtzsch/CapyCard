using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data; // NEU
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore; // NEU
using System;
using System.Collections.ObjectModel;
using System.Linq; // NEU
using System.Threading.Tasks; // NEU

namespace FlashcardApp.ViewModels
{
    public partial class CardListViewModel : ObservableObject
    {
        // NEU: Eigenen DBContext hinzugefügt, um Lösch-Operationen durchzuführen
        private readonly FlashcardDbContext _dbContext;
        private Deck? _currentDeck;

        [ObservableProperty]
        private string _deckName = "Karten";

        public ObservableCollection<Card> Cards { get; } = new();

        public event Action? OnNavigateBack;

        public CardListViewModel()
        {
            // NEU: DBContext initialisieren
            _dbContext = new FlashcardDbContext();
        }

        // NEU: Signatur geändert. Lädt Karten jetzt selbst basierend auf dem Deck.
        public async void LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = $"Karten für: {deck.Name}";
            
            Cards.Clear();
            // NEU: Lädt Karten aus der DB
            var cardsFromDb = await _dbContext.Cards
                                .Where(c => c.DeckId == _currentDeck.Id)
                                .ToListAsync();

            foreach (var card in cardsFromDb)
            {
                Cards.Add(card);
            }
        }

        // NEU: Befehl zum sofortigen Löschen einer Karte
        [RelayCommand]
        private async Task DeleteCard(Card? card)
        {
            if (card == null) return;
            
            _dbContext.Cards.Remove(card);
            await _dbContext.SaveChangesAsync();
            
            Cards.Remove(card); // Aus der UI-Liste entfernen
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
    }
}