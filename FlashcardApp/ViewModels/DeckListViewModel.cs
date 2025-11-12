using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    public partial class DeckListViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;

        [ObservableProperty]
        private string _newDeckName = string.Empty;

        // NEU: Diese Eigenschaft steuert die Sichtbarkeit der Bestätigungs-Box
        [ObservableProperty]
        private bool _isConfirmingDelete = false;

        // NEU: Hier merken wir uns, welches Fach gelöscht werden soll
        private Deck? _deckToConfirmDelete;

        private Deck? _selectedDeck;
        public Deck? SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                if (SetProperty(ref _selectedDeck, value) && value != null)
                {
                    OnDeckSelected?.Invoke(value);
                    _selectedDeck = null;
                    OnPropertyChanged(nameof(SelectedDeck));
                }
            }
        }

        public event Action<Deck>? OnDeckSelected;

        public ObservableCollection<Deck> Decks { get; } = new();

        public DeckListViewModel()
        {
            _dbContext = new FlashcardDbContext();
            LoadDecks();
        }

        private async void LoadDecks()
        {
            Decks.Clear();
            var decksFromDb = await _dbContext.Decks.ToListAsync();
            foreach (var deck in decksFromDb)
            {
                Decks.Add(deck);
            }
        }

        [RelayCommand]
        private async Task AddDeck()
        {
            if (string.IsNullOrWhiteSpace(NewDeckName))
            {
                return;
            }
            var newDeck = new Deck { Name = NewDeckName };
            _dbContext.Decks.Add(newDeck);
            await _dbContext.SaveChangesAsync();
            Decks.Add(newDeck);
            NewDeckName = string.Empty;
        }

        // NEU: Dieser Befehl wird vom "Löschen"-Button in der Liste aufgerufen
        [RelayCommand]
        private void DeleteDeck(Deck? deck)
        {
            if (deck == null) return;
            
            _deckToConfirmDelete = deck;
            IsConfirmingDelete = true; // Zeigt die Bestätigungs-Box an
        }

        // NEU: Dieser Befehl wird vom "Ja, löschen"-Button in der Box aufgerufen
        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (_deckToConfirmDelete == null) return;

            // WICHTIG: Da wir 'Cascade' in der DB eingestellt haben,
            // werden alle Karten, die zu diesem Deck gehören, automatisch mitgelöscht.
            _dbContext.Decks.Remove(_deckToConfirmDelete);
            await _dbContext.SaveChangesAsync();

            Decks.Remove(_deckToConfirmDelete); // Aus der UI-Liste entfernen

            // Reset
            _deckToConfirmDelete = null;
            IsConfirmingDelete = false;
        }

        // NEU: Dieser Befehl wird vom "Abbrechen"-Button in der Box aufgerufen
        [RelayCommand]
        private void CancelDelete()
        {
            _deckToConfirmDelete = null;
            IsConfirmingDelete = false; // Versteckt die Bestätigungs-Box
        }
    }
}