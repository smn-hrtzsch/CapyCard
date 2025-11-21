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
    // Helper class for grouping cards
    public partial class CardGroupViewModel : ObservableObject
    {
        public string Title { get; }
        public ObservableCollection<CardItemViewModel> Cards { get; } = new();
        
        [ObservableProperty]
        private bool _isExpanded = false;

        public CardGroupViewModel(string title)
        {
            Title = title;
        }
    }

    public partial class CardListViewModel : ObservableObject
    {
        private Deck? _currentDeck;

        [ObservableProperty]
        private string _deckName = "Karten";

        // Changed from flat list to grouped list
        public ObservableCollection<CardGroupViewModel> CardGroups { get; } = new();
        
        // Helper to access all cards flat for selection logic
        private IEnumerable<CardItemViewModel> AllCards => CardGroups.SelectMany(g => g.Cards);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPdfButton))]
        [NotifyPropertyChangedFor(nameof(SelectAllButtonText))]
        private int _selectedCardCount = 0;

        [ObservableProperty]
        private string _backButtonText = "Zurück zum Fach";

        public bool ShowPdfButton => SelectedCardCount > 0;
        public string SelectAllButtonText
        {
            get
            {
                var activeGroup = CardGroups.FirstOrDefault(g => g.Cards.Any(c => c.IsSelected));
                if (activeGroup != null)
                {
                    // If all cards in the active group are selected -> "Alle abwählen"
                    // Otherwise -> "Alle auswählen"
                    return activeGroup.Cards.All(c => c.IsSelected) ? "Alle abwählen" : "Alle auswählen";
                }
                return "Alle auswählen";
            }
        }
        
        [ObservableProperty] 
        private List<int> _columnOptions = new() { 1, 2, 3, 4, 5 };

        [ObservableProperty] 
        private int _selectedColumnCount = 3;

        public event Action? OnNavigateBack;
        public event Action<Deck, Card>? OnEditCardRequest;

        // NEU: Event für den "Speichern unter"-Dialog.
        // Input: string (vorgeschlagener Name), Output: Task<string?> (gewählter Pfad)
        public event Func<string, Task<string?>>? ShowSaveFileDialog;

        // Store expansion state per deck (DeckId -> Set of expanded group titles)
        private Dictionary<int, HashSet<string>> _deckExpansionStates = new();

        public CardListViewModel()
        {
            // _dbContext removed
        }

        public async void LoadDeck(Deck deck)
        {
            // Save state of current deck before switching
            if (_currentDeck != null)
            {
                var expandedTitles = CardGroups.Where(g => g.IsExpanded).Select(g => g.Title).ToHashSet();
                if (_deckExpansionStates.ContainsKey(_currentDeck.Id))
                {
                    _deckExpansionStates[_currentDeck.Id] = expandedTitles;
                }
                else
                {
                    _deckExpansionStates.Add(_currentDeck.Id, expandedTitles);
                }
            }

            _currentDeck = deck;
            DeckName = $"Karten für: {deck.Name}";
            
            // Set Back Button Text based on context
            BackButtonText = deck.ParentDeckId != null ? "Zurück zum Thema" : "Zurück zum Fach";

            // Cleanup old handlers
            foreach (var item in AllCards)
            {
                item.PropertyChanged -= CardItem_PropertyChanged;
            }
            CardGroups.Clear();

            // Get saved state for new deck
            HashSet<string> savedState = new();
            if (_deckExpansionStates.ContainsKey(deck.Id))
            {
                savedState = _deckExpansionStates[deck.Id];
            }
            
            using (var context = new FlashcardDbContext())
            {
                // Load current deck cards (General)
                var currentDeckCards = await context.Cards
                                    .AsNoTracking() 
                                    .Where(c => c.DeckId == _currentDeck.Id)
                                    .ToListAsync();

                if (currentDeckCards.Any())
                {
                    // If it is a subdeck, use the deck name as group title, otherwise "Allgemein"
                    string groupTitle = (_currentDeck.ParentDeckId != null) ? _currentDeck.Name : "Allgemein";
                    var generalGroup = new CardGroupViewModel(groupTitle);
                    
                    // Restore expansion state
                    // Force expansion if it is a subdeck (single group view)
                    if (_currentDeck.ParentDeckId != null)
                    {
                        generalGroup.IsExpanded = true;
                    }
                    else
                    {
                        generalGroup.IsExpanded = savedState.Contains(generalGroup.Title);
                    }
                    
                    foreach (var card in currentDeckCards)
                    {
                        var itemVM = new CardItemViewModel(card);
                        itemVM.PropertyChanged += CardItem_PropertyChanged;
                        generalGroup.Cards.Add(itemVM);
                    }
                    CardGroups.Add(generalGroup);
                }

                // Load subdecks recursively (flattened for now or grouped by subdeck)
                // Requirement: "When opening a main deck -> Show 'General Cards' section and then sections for each subdeck."
                // We need to fetch subdecks.
                var subDecks = await context.Decks
                    .AsNoTracking()
                    .Include(d => d.Cards)
                    .Where(d => d.ParentDeckId == _currentDeck.Id)
                    .OrderByDescending(d => d.Name == "Allgemein") // Allgemein first
                    .ThenBy(d => d.Id)
                    .ToListAsync();

                foreach (var subDeck in subDecks)
                {
                    if (subDeck.Cards.Any())
                    {
                        var subGroup = new CardGroupViewModel(subDeck.Name);
                        // Restore expansion state
                        subGroup.IsExpanded = savedState.Contains(subGroup.Title);

                        foreach (var card in subDeck.Cards)
                        {
                            var itemVM = new CardItemViewModel(card);
                            itemVM.PropertyChanged += CardItem_PropertyChanged;
                            subGroup.Cards.Add(itemVM);
                        }
                        CardGroups.Add(subGroup);
                    }
                }
            }

            UpdateSelectedCount();
        }

        [RelayCommand]
        private async Task DeleteCard(CardItemViewModel? itemVM)
        {
            if (itemVM == null) return;
            
            itemVM.PropertyChanged -= CardItem_PropertyChanged;
            
            using (var context = new FlashcardDbContext())
            {
                // Attach and remove
                context.Cards.Attach(itemVM.Card);
                context.Cards.Remove(itemVM.Card);
                await context.SaveChangesAsync();
            }
            
            // Remove from UI
            foreach (var group in CardGroups)
            {
                if (group.Cards.Contains(itemVM))
                {
                    group.Cards.Remove(itemVM);
                    break;
                }
            }
            
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
            // Find the group that currently has selected cards
            var activeGroup = CardGroups.FirstOrDefault(g => g.Cards.Any(c => c.IsSelected));

            if (activeGroup != null)
            {
                // Toggle selection for this group only
                // If all are selected, deselect all. Otherwise select all.
                bool shouldSelect = !activeGroup.Cards.All(c => c.IsSelected);
                
                foreach (var item in activeGroup.Cards)
                {
                    item.IsSelected = shouldSelect;
                }
                
                // Ensure group is expanded if we are selecting
                if (shouldSelect)
                {
                    activeGroup.IsExpanded = true;
                }
            }
            else
            {
                // If no cards are selected, select all in the first visible group (if any)
                var firstGroup = CardGroups.FirstOrDefault();
                if (firstGroup != null)
                {
                    foreach (var item in firstGroup.Cards)
                    {
                        item.IsSelected = true;
                    }
                    firstGroup.IsExpanded = true;
                }
            }
            UpdateSelectedCount();
        }

        // KORREKTUR: Befehl ist jetzt 'async' und implementiert
        [RelayCommand]
        private async Task GeneratePdf()
        {
            var activeGroup = CardGroups.FirstOrDefault(g => g.Cards.Any(c => c.IsSelected));
            var selectedCards = activeGroup?.Cards
                .Where(c => c.IsSelected)
                .Select(c => c.Card)
                .ToList() ?? new List<Card>();

            // 1. Prüfen, ob Karten ausgewählt sind und der Dialog-Handler existiert
            if (!selectedCards.Any() || ShowSaveFileDialog == null)
            {
                return;
            }

            // 2. Vorgeschlagenen Dateinamen festlegen
            string suggestedName = "Karten.pdf";
            if (_currentDeck != null && activeGroup != null)
            {
                if (activeGroup.Title == "Allgemein")
                {
                    suggestedName = $"{_currentDeck.Name}-Allgemein.pdf";
                }
                else
                {
                    suggestedName = $"{activeGroup.Title}.pdf";
                }
            }

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
                if (sender is CardItemViewModel item && item.IsSelected)
                {
                    // Enforce single group selection
                    // Find the group this item belongs to
                    var ownerGroup = CardGroups.FirstOrDefault(g => g.Cards.Contains(item));
                    if (ownerGroup != null)
                    {
                        // Deselect all cards in other groups
                        foreach (var group in CardGroups)
                        {
                            if (group != ownerGroup)
                            {
                                foreach (var otherItem in group.Cards)
                                {
                                    if (otherItem.IsSelected) otherItem.IsSelected = false;
                                }
                            }
                        }
                    }
                }
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCardCount = AllCards.Count(c => c.IsSelected);
        }
    }
}