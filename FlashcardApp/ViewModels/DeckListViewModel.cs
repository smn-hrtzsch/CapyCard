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
    public partial class DeckListViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;

        [ObservableProperty]
        private string _newDeckName = string.Empty;

        [ObservableProperty]
        private bool _isConfirmingDelete = false;

        // KORREKTUR: Speichert jetzt das DeckItemViewModel für die Lösch-Bestätigung
        private DeckItemViewModel? _deckToConfirmDelete;

        // KORREKTUR: Die Liste verwaltet jetzt DeckItemViewModels
        public ObservableCollection<DeckItemViewModel> Decks { get; } = new();

        
        // KORREKTUR: Die Eigenschaft für das ausgewählte Item ist jetzt vom Typ DeckItemViewModel
        private DeckItemViewModel? _selectedDeck;
        public DeckItemViewModel? SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                // Beim Auswählen eines Items (Klick für Navigation)...
                if (SetProperty(ref _selectedDeck, value) && value != null)
                {
                    // ...solange wir nicht gerade dieses Item bearbeiten...
                    if (!value.IsEditing)
                    {
                        // ...navigiere zur Detail-Ansicht.
                        OnDeckSelected?.Invoke(value.Deck);
                        _selectedDeck = null; // Auswahl aufheben
                        OnPropertyChanged(nameof(SelectedDeck));
                    }
                }
            }
        }

        public event Action<Deck>? OnDeckSelected;


        public DeckListViewModel()
        {
            _dbContext = new FlashcardDbContext();
            LoadDecks();
        }

        private async void LoadDecks()
        {
            Decks.Clear();
            var decksFromDb = await _dbContext.Decks
                .Select(d => new { Deck = d, CardCount = d.Cards.Count })
                .ToListAsync();

            foreach (var entry in decksFromDb)
            {
                // KORREKTUR: Füge den Wrapper (DeckItemViewModel) zur Liste hinzu
                Decks.Add(new DeckItemViewModel(entry.Deck, entry.CardCount));
            }
        }

        public void UpdateDeckCardCount(int deckId, int cardCount)
        {
            var deckVm = Decks.FirstOrDefault(d => d.Deck.Id == deckId);
            if (deckVm != null)
            {
                deckVm.CardCount = cardCount;
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
            
            // KORREKTUR: Füge den Wrapper zur UI-Liste hinzu
            Decks.Add(new DeckItemViewModel(newDeck));
            NewDeckName = string.Empty;
        }

        // KORREKTUR: Nimmt jetzt ein DeckItemViewModel entgegen
        [RelayCommand]
        private void DeleteDeck(DeckItemViewModel? itemVM)
        {
            if (itemVM == null) return;
            
            _deckToConfirmDelete = itemVM;
            IsConfirmingDelete = true;
        }

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (_deckToConfirmDelete == null) return;

            // KORREKTUR: Löscht das 'innere' Deck-Modell
            _dbContext.Decks.Remove(_deckToConfirmDelete.Deck);
            await _dbContext.SaveChangesAsync();

            // KORREKTUR: Entfernt das Wrapper-ViewModel aus der UI-Liste
            Decks.Remove(_deckToConfirmDelete);

            _deckToConfirmDelete = null;
            IsConfirmingDelete = false;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            _deckToConfirmDelete = null;
            IsConfirmingDelete = false; 
        }

        // NEU: Befehl zum Speichern des bearbeiteten Fachnamens
        [RelayCommand]
        private async Task SaveDeckEdit(DeckItemViewModel? itemVM)
        {
            if (itemVM == null || string.IsNullOrWhiteSpace(itemVM.EditText))
            {
                itemVM?.CancelEdit(); // Breche ab, wenn der Name leer ist
                SelectedDeck = null;
                return;
            }

            // Finde das Fach in der Datenbank
            var trackedDeck = await _dbContext.Decks.FindAsync(itemVM.Deck.Id);
            if (trackedDeck != null)
            {
                // 1. Aktualisiere die Datenbank
                trackedDeck.Name = itemVM.EditText;
                await _dbContext.SaveChangesAsync();

                // 2. Aktualisiere die "Name"-Eigenschaft im UI-Modell
                //    (Dadurch wird das TextBlock in der UI aktualisiert)
                itemVM.Name = itemVM.EditText;

                // 3. Beende den Bearbeiten-Modus
                itemVM.IsEditing = false;
            }
            else
            {
                // Fach nicht gefunden? Breche den Editiermodus ab.
                itemVM.CancelEdit();
            }

            SelectedDeck = null;
        }
    }
}