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
            // _dbContext removed
            LoadDecks();
        }

        public void RefreshDecks()
        {
            LoadDecks();
        }

        private async void LoadDecks()
        {
            Decks.Clear();
            
            using (var context = new FlashcardDbContext())
            {
                // Load all decks to build hierarchy in memory (simpler for now than recursive SQL)
                var allDecks = await context.Decks
                    .AsNoTracking()
                    .Include(d => d.Cards)
                    .ToListAsync();

                var rootDecks = allDecks.Where(d => d.ParentDeckId == null).ToList();

                foreach (var deck in rootDecks)
                {
                    var vm = CreateDeckItemViewModel(deck, allDecks);
                    Decks.Add(vm);
                }
            }
        }

        private DeckItemViewModel CreateDeckItemViewModel(Deck deck, System.Collections.Generic.List<Deck> allDecks)
        {
            // Calculate total cards recursively
            int totalCards = CalculateTotalCards(deck, allDecks);
            var vm = new DeckItemViewModel(deck, totalCards);
            
            var subDecks = allDecks
                .Where(d => d.ParentDeckId == deck.Id)
                .OrderByDescending(d => d.Name == "Allgemein") // Allgemein first
                .ThenBy(d => d.Id)
                .ToList();

            if (subDecks.Any())
            {
                vm.HasSubDecks = true;
                foreach (var subDeck in subDecks)
                {
                    vm.SubDecks.Add(CreateDeckItemViewModel(subDeck, allDecks));
                }
            }
            
            return vm;
        }

        private int CalculateTotalCards(Deck deck, System.Collections.Generic.List<Deck> allDecks)
        {
            int count = deck.Cards.Count;
            var subDecks = allDecks.Where(d => d.ParentDeckId == deck.Id);
            foreach (var sub in subDecks)
            {
                count += CalculateTotalCards(sub, allDecks);
            }
            return count;
        }

        public void UpdateDeckCardCount(int deckId, int cardCount)
        {
            // Recursive search
            var deckVm = FindDeckViewModel(Decks, deckId);
            if (deckVm != null)
            {
                deckVm.CardCount = cardCount;
            }
        }

        private DeckItemViewModel? FindDeckViewModel(ObservableCollection<DeckItemViewModel> list, int deckId)
        {
            foreach (var item in list)
            {
                if (item.Deck.Id == deckId) return item;
                var found = FindDeckViewModel(item.SubDecks, deckId);
                if (found != null) return found;
            }
            return null;
        }

        [RelayCommand]
        private async Task AddDeck()
        {
            if (string.IsNullOrWhiteSpace(NewDeckName))
            {
                return;
            }
            var newDeck = new Deck { Name = NewDeckName };
            
            using (var context = new FlashcardDbContext())
            {
                context.Decks.Add(newDeck);
                await context.SaveChangesAsync();
            }
            
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

            using (var context = new FlashcardDbContext())
            {
                // KORREKTUR: Löscht das 'innere' Deck-Modell
                // Wir müssen es erst attachen oder finden
                var deckToDelete = await context.Decks.FindAsync(_deckToConfirmDelete.Deck.Id);
                if (deckToDelete != null)
                {
                    context.Decks.Remove(deckToDelete);
                    await context.SaveChangesAsync();
                }
            }

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

            using (var context = new FlashcardDbContext())
            {
                // Finde das Fach in der Datenbank
                var trackedDeck = await context.Decks.FindAsync(itemVM.Deck.Id);
                if (trackedDeck != null)
                {
                    // 1. Aktualisiere die Datenbank
                    trackedDeck.Name = itemVM.EditText;
                    await context.SaveChangesAsync();

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
            }

            SelectedDeck = null;
        }

        [RelayCommand]
        private void SelectSubDeck(DeckItemViewModel? subDeckVM)
        {
            if (subDeckVM != null)
            {
                OnDeckSelected?.Invoke(subDeckVM.Deck);
            }
        }
    }
}