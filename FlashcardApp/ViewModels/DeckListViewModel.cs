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
    // Das ist die Logik für die Ansicht "Fächer-Liste"
    public partial class DeckListViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;

        [ObservableProperty]
        private string _newDeckName = string.Empty;

        // Diese Eigenschaft wird an die ListBox.SelectedItem gebunden
        private Deck? _selectedDeck;
        public Deck? SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                // Wenn der Wert sich ändert UND nicht null ist...
                if (SetProperty(ref _selectedDeck, value) && value != null)
                {
                    // ...feuere das Event, um die Navigation auszulösen
                    OnDeckSelected?.Invoke(value);
                    
                    // Setze die Auswahl in der UI sofort zurück (auf null),
                    // damit der Benutzer dasselbe Fach erneut anklicken kann.
                    _selectedDeck = null;
                    OnPropertyChanged(nameof(SelectedDeck));
                }
            }
        }

        // Dieses Event fängt das MainViewModel ab, um die Seite zu wechseln
        public event Action<Deck>? OnDeckSelected;

        // Die Liste der Fächer, die in der UI angezeigt wird
        public ObservableCollection<Deck> Decks { get; } = new();

        public DeckListViewModel()
        {
            _dbContext = new FlashcardDbContext();
            // Lade Fächer aus der DB, sobald das ViewModel erstellt wird
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
                // (Hier könnten wir später eine Fehlermeldung anzeigen)
                return;
            }

            var newDeck = new Deck { Name = NewDeckName };

            // In DB speichern
            _dbContext.Decks.Add(newDeck);
            await _dbContext.SaveChangesAsync();

            // Zur UI-Liste hinzufügen
            Decks.Add(newDeck);

            // Textfeld leeren
            NewDeckName = string.Empty;
        }
    }
}