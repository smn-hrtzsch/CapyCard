using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public ObservableCollection<CardItemViewModel> Cards { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPdfButton))]
        [NotifyPropertyChangedFor(nameof(SelectAllButtonText))]
        private int _selectedCardCount = 0;

        public bool ShowPdfButton => SelectedCardCount > 0;
        public string SelectAllButtonText => SelectedCardCount == Cards.Count && Cards.Count > 0
            ? "Alle abwählen"
            : "Alle auswählen";
        
        [ObservableProperty] 
        private List<int> _columnOptions = new() { 1, 2, 3, 4, 5 };

        [ObservableProperty] 
        private int _selectedColumnCount = 3;

        public event Action? OnNavigateBack;
        public event Action<Deck, Card>? OnEditCardRequest;

        // NEU: Event für den "Speichern unter"-Dialog.
        // Input: string (vorgeschlagener Name), Output: Task<string?> (gewählter Pfad)
        public event Func<string, Task<string?>>? ShowSaveFileDialog;

        public CardListViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async void LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = $"Karten für: {deck.Name}";
            
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
                var itemVM = new CardItemViewModel(card);
                itemVM.PropertyChanged += CardItem_PropertyChanged;
                Cards.Add(itemVM);
            }
            UpdateSelectedCount();
        }

        [RelayCommand]
        private async Task DeleteCard(CardItemViewModel? itemVM)
        {
            if (itemVM == null) return;
            
            itemVM.PropertyChanged -= CardItem_PropertyChanged;
            _dbContext.Cards.Attach(itemVM.Card);
            _dbContext.Cards.Remove(itemVM.Card);
            await _dbContext.SaveChangesAsync();
            
            Cards.Remove(itemVM);
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void EditCard(CardItemViewModel? itemVM)
        {
            if (itemVM != null && _currentDeck != null)
            {
                OnEditCardRequest?.Invoke(_currentDeck, itemVM.Card);
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            OnNavigateBack?.Invoke();
        }

        [RelayCommand]
        private void ToggleSelectAll()
        {
            bool selectAll = Cards.Count == 0 || Cards.Any(c => !c.IsSelected);
            foreach (var item in Cards)
            {
                item.IsSelected = selectAll;
            }
            UpdateSelectedCount();
        }

        // KORREKTUR: Befehl ist jetzt 'async' und implementiert
        [RelayCommand]
        private async Task GeneratePdf()
        {
            var selectedCards = Cards
                .Where(c => c.IsSelected)
                .Select(c => c.Card)
                .ToList();

            // 1. Prüfen, ob Karten ausgewählt sind und der Dialog-Handler existiert
            if (!selectedCards.Any() || ShowSaveFileDialog == null)
            {
                return;
            }

            // 2. Vorgeschlagenen Dateinamen festlegen
            string suggestedName = $"{_currentDeck?.Name ?? "Karten"}.pdf";

            try
            {
                // 3. Den "Speichern unter"-Dialog aufrufen (wird von der View behandelt)
                string? path = await ShowSaveFileDialog.Invoke(suggestedName);

                // 4. Wenn der Nutzer einen Pfad ausgewählt hat (nicht auf "Abbrechen" geklickt hat)
                if (!string.IsNullOrEmpty(path))
                {
                    // 5. PDF generieren und speichern
                    PdfGenerationService.GeneratePdf(path, selectedCards, SelectedColumnCount);
                }
            }
            catch (Exception ex)
            {
                // TODO: Hier könnten wir dem Nutzer eine Fehlermeldung anzeigen
                Console.WriteLine($"PDF-Speicherfehler: {ex.Message}");
            }
        }

        private void CardItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CardItemViewModel.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCardCount = Cards.Count(c => c.IsSelected);
        }
    }
}