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
    public partial class CardListViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private Deck? _currentDeck;

        [ObservableProperty]
        private string _deckName = "Karten";

        public ObservableCollection<Card> Cards { get; } = new();

        public event Action? OnNavigateBack;
        public event Action<Deck, Card>? OnEditCardRequest;

        public CardListViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async void LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = $"Karten für: {deck.Name}";
            
            Cards.Clear();
            
            // --- HIER IST DIE KORREKTUR ---
            // 'AsNoTracking()' zwingt EF Core, die Datenbank-Datei
            // neu abzufragen, anstatt veraltete Daten aus dem Cache anzuzeigen.
            var cardsFromDb = await _dbContext.Cards
                                .AsNoTracking() 
                                .Where(c => c.DeckId == _currentDeck.Id)
                                .ToListAsync();

            foreach (var card in cardsFromDb)
            {
                Cards.Add(card);
            }
        }

        [RelayCommand]
        private async Task DeleteCard(Card? card)
        {
            if (card == null) return;
            
            // Fürs Löschen müssen wir die Entität erst 'attachen',
            // da der Context sie (wegen AsNoTracking) nicht kennt.
            _dbContext.Cards.Attach(card);
            _dbContext.Cards.Remove(card);
            await _dbContext.SaveChangesAsync();
            
            Cards.Remove(card); 
        }

        [RelayCommand]
        private void EditCard(Card? card)
        {
            if (card != null && _currentDeck != null)
            {
                OnEditCardRequest?.Invoke(_currentDeck, card);
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }
    }
}