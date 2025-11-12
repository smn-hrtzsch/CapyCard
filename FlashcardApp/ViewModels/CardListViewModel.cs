using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel; // Hinzugefügt für PropertyChangedEventArgs
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

        // KORREKTUR: Die Liste verwaltet jetzt CardItemViewModels
        public ObservableCollection<CardItemViewModel> Cards { get; } = new();

        // NEU: Zählt die ausgewählten Karten
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPdfButton))] // Aktualisiert den PDF-Button
        private int _selectedCardCount = 0;

        // NEU: Steuert die Sichtbarkeit des PDF-Buttons
        public bool ShowPdfButton => SelectedCardCount > 0;

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
            
            // Alte Listener entfernen, bevor wir die Liste leeren
            foreach (var item in Cards)
            {
                item.PropertyChanged -= CardItem_PropertyChanged;
            }
            Cards.Clear();
            
            var cardsFromDb = await _dbContext.Cards
                                .AsNoTracking() 
                                .Where(c => c.DeckId == _currentDeck.Id)
                                .ToListAsync();

            foreach (var card in cardsFromDb)
            {
                // KORREKTUR: Füge den Wrapper (CardItemViewModel) hinzu
                var itemVM = new CardItemViewModel(card);
                
                // NEU: Wir hören auf Änderungen der 'IsSelected'-Eigenschaft
                itemVM.PropertyChanged += CardItem_PropertyChanged;
                
                Cards.Add(itemVM);
            }
            UpdateSelectedCount(); // Zähler beim Laden initialisieren
        }

        // KORREKTUR: Nimmt jetzt ein CardItemViewModel entgegen
        [RelayCommand]
        private async Task DeleteCard(CardItemViewModel? itemVM)
        {
            if (itemVM == null) return;
            
            // NEU: Listener entfernen, um Speicherlecks zu vermeiden
            itemVM.PropertyChanged -= CardItem_PropertyChanged;

            // KORREKTUR: Löscht die 'innere' Karte
            _dbContext.Cards.Attach(itemVM.Card);
            _dbContext.Cards.Remove(itemVM.Card);
            await _dbContext.SaveChangesAsync();
            
            Cards.Remove(itemVM); // Entfernt den Wrapper aus der UI
            UpdateSelectedCount(); // Zähler aktualisieren
        }

        // KORREKTUR: Nimmt jetzt ein CardItemViewModel entgegen
        [RelayCommand]
        private void EditCard(CardItemViewModel? itemVM)
        {
            if (itemVM != null && _currentDeck != null)
            {
                // KORREKTUR: Übergibt die 'innere' Karte
                OnEditCardRequest?.Invoke(_currentDeck, itemVM.Card);
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }

        // NEU: Befehl für den "Alle auswählen"-Button
        [RelayCommand]
        private void ToggleSelectAll()
        {
            // Wenn NICHT alle ausgewählt sind, wähle alle aus.
            // Wenn alle ausgewählt sind, wähle alle ab.
            bool selectAll = Cards.Count == 0 || Cards.Any(c => !c.IsSelected);
            
            foreach (var item in Cards)
            {
                item.IsSelected = selectAll;
            }
            // (Der Zähler wird automatisch durch die PropertyChanged-Events aktualisiert)
        }

        // NEU: Befehl für den "Als PDF speichern"-Button
        [RelayCommand]
        private void GeneratePdf()
        {
            // Dieser Befehl ist noch leer.
            // In Teil 2 rufen wir von hier aus den Speichern-Dialog
            // und den PdfGenerationService auf.
            
            // Logik (für später):
            // var selectedCards = Cards.Where(c => c.IsSelected).Select(c => c.Card).ToList();
            // Console.WriteLine($"Würde jetzt {selectedCards.Count} Karten als PDF speichern.");
        }

        // NEU: Dieser Handler wird aufgerufen, wenn eine Checkbox geklickt wird
        private void CardItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CardItemViewModel.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        // NEU: Hilfsmethode zur Aktualisierung des Zählers
        private void UpdateSelectedCount()
        {
            SelectedCardCount = Cards.Count(c => c.IsSelected);
        }
    }
}