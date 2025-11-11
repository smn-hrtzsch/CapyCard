using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel; // Wichtig für MVVM
using CommunityToolkit.Mvvm.Input;       // Wichtig für Commands (Buttons)
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel; // Wichtig für Listen, die die UI aktualisieren
using System.Linq;
using System.Threading.Tasks;

namespace FlashcardApp.ViewModels
{
    // ObservableObject ist die Basisklasse, die INotifyPropertyChanged implementiert
    // (sorgt dafür, dass die UI Änderungen mitbekommt)
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;

        // [ObservableProperty] erstellt automatisch das Property "NewDeckName"
        // und benachrichtigt die UI bei Änderungen.
        [ObservableProperty]
        private string _newDeckName = string.Empty;

        // Das ist die Liste, an die wir unsere UI binden werden.
        public ObservableCollection<Deck> Decks { get; } = new();

        public MainWindowViewModel()
        {
            _dbContext = new FlashcardDbContext();

            // Lädt Decks beim Start
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

        // [RelayCommand] erstellt automatisch den "AddDeckCommand"
        // für unseren Button.
        [RelayCommand]
        private async Task AddDeck()
        {
            if (string.IsNullOrWhiteSpace(NewDeckName))
            {
                // Hier könnten wir später eine Fehlermeldung anzeigen
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