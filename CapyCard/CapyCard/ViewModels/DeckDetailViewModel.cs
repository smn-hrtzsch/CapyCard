using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Data;
using CapyCard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace CapyCard.ViewModels
{
    public partial class DeckDetailViewModel : ObservableObject
    {
        private Deck? _currentDeck;
        private Card? _cardToEdit;

        [ObservableProperty]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        private string _newCardBack = string.Empty;

        [ObservableProperty]
        private string _deckName = "Fach laden...";

        [ObservableProperty]
        private string _backButtonText = "Zurück zur Fächerliste";

        [ObservableProperty]
        private string _cardCountText = "Karten anzeigen (0)";
        
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GoToCardListCommand))] 
        private bool _hasCards = false;

        [ObservableProperty]
        private string _saveButtonText = "Karte hinzufügen";

        [ObservableProperty]
        private bool _isEditing = false;

        [ObservableProperty]
        private string _newSubDeckName = string.Empty;

        [ObservableProperty]
        private bool _canAddSubDecks = false;

        [ObservableProperty]
        private bool _isSubDeckSelectionVisible = false;

        [ObservableProperty]
        private bool _isRootDeck = false;

        [ObservableProperty]
        private bool _isConfirmingDeleteSubDeck = false;

        private DeckItemViewModel? _subDeckToConfirmDelete;

        public ObservableCollection<Card> Cards { get; } = new();
        public ObservableCollection<DeckItemViewModel> SubDecks { get; } = new();
        public ObservableCollection<SubDeckSelectionItem> SubDeckSelectionList { get; } = new();

        public event Action? OnNavigateBack;
        public event Action<Deck>? OnNavigateToCardList; 
        public event Action<Deck, LearningMode, List<int>?>? OnNavigateToLearn; // Updated signature
        public event Action<Deck>? OnNavigateToDeck; // New event for subdeck navigation
        public event Action<Deck, int>? OnCardCountUpdated;
        public event Action? OnSubDeckAdded; // New event for subdeck addition
        public event Action? RequestFrontFocus;
        
        /// <summary>
        /// Func to get pending images from the View's RichTextEditors.
        /// Returns a list of CardImage objects that need to be saved.
        /// </summary>
        public Func<List<CardImage>>? GetPendingImages;

        public DeckDetailViewModel()
        {
            // _dbContext removed. We use short-lived contexts now.
        }

        public async Task LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            
            using (var context = new FlashcardDbContext())
            {
                // Set Title and Back Button based on context
                if (deck.ParentDeckId != null)
                {
                    var parent = await context.Decks.FindAsync(deck.ParentDeckId);
                    DeckName = parent != null ? $"{parent.Name} > {deck.Name}" : deck.Name;
                    BackButtonText = "Zurück zum Hauptfach";
                }
                else
                {
                    DeckName = deck.Name;
                    BackButtonText = "Zurück zur Fächerliste";
                }
            }
            
            ResetToAddingMode(); 
            await RefreshCardDataAsync();
        }
        
        public async Task LoadCardForEditing(Deck deck, Card card)
        {
            _currentDeck = deck;
            _cardToEdit = card;
            
            using (var context = new FlashcardDbContext())
            {
                // Set Title based on context (same logic)
                if (deck.ParentDeckId != null)
                {
                    var parent = await context.Decks.FindAsync(deck.ParentDeckId);
                    DeckName = parent != null ? $"{parent.Name} > {deck.Name}" : deck.Name;
                    BackButtonText = "Zurück zum Hauptfach";
                }
                else
                {
                    DeckName = deck.Name;
                    BackButtonText = "Zurück zur Fächerliste";
                }
            }

            NewCardFront = card.Front;
            NewCardBack = card.Back;
            
            SaveButtonText = "Änderungen speichern";
            IsEditing = true;
            
            await RefreshCardDataAsync();
        }

        public async Task RefreshCardDataAsync()
        {
            if (_currentDeck == null) return;
            
            using (var context = new FlashcardDbContext())
            {
                var deckFromDb = await context.Decks
                                     .AsNoTracking() 
                                     .Include(d => d.Cards)
                                     .Include(d => d.SubDecks)
                                     .ThenInclude(sd => sd.Cards) // Include cards of subdecks for count
                                     .FirstOrDefaultAsync(d => d.Id == _currentDeck.Id);
                
                if (deckFromDb == null) 
                {
                    GoBack();
                    return; 
                }
                
                _currentDeck = deckFromDb; 
                CanAddSubDecks = _currentDeck.ParentDeckId == null;
                IsRootDeck = _currentDeck.ParentDeckId == null;
                
                if (!IsEditing)
                {
                    SaveButtonText = IsRootDeck ? "Allgemeine Karte hinzufügen" : "Karte zu Thema hinzufügen";
                }

                Cards.Clear();
                foreach (var card in deckFromDb.Cards.OrderBy(c => c.Id))
                {
                    Cards.Add(card);
                }

                SubDecks.Clear();
                var sortedSubDecks = deckFromDb.SubDecks
                    .OrderByDescending(d => d.IsDefault)
                    .ThenByDescending(d => d.Name == "Allgemein") 
                    .ThenBy(d => d.Id);

                foreach (var subDeck in sortedSubDecks)
                {
                    // Wrap in ViewModel
                    var vm = new DeckItemViewModel(subDeck, subDeck.Cards.Count);
                    
                    // "Allgemein" oder Default-Decks dürfen nicht bearbeitet/gelöscht werden
                    if (subDeck.IsDefault || subDeck.Name == "Allgemein")
                    {
                        vm.IsStatic = true;
                    }
                    
                    SubDecks.Add(vm);
                }
                
                int totalCards = Cards.Count + SubDecks.Sum(sd => sd.Deck.Cards.Count);
                UpdateCardCount(totalCards);
            }
        }
        
        [RelayCommand]
        private async Task AddSubDeck()
        {
            if (string.IsNullOrWhiteSpace(NewSubDeckName) || _currentDeck == null || _currentDeck.ParentDeckId != null) return;

            using (var context = new FlashcardDbContext())
            {
                var newDeck = new Deck
                {
                    Name = NewSubDeckName,
                    ParentDeckId = _currentDeck.Id
                };
                context.Decks.Add(newDeck);
                await context.SaveChangesAsync();
            }
            
            NewSubDeckName = string.Empty;
            await RefreshCardDataAsync();
            OnSubDeckAdded?.Invoke();
        }

        [RelayCommand]
        private void OpenSubDeck(DeckItemViewModel subDeckVM)
        {
            OnNavigateToDeck?.Invoke(subDeckVM.Deck);
        }

        [RelayCommand]
        private async Task SaveSubDeckEdit(DeckItemViewModel? itemVM)
        {
            if (itemVM == null || string.IsNullOrWhiteSpace(itemVM.EditText))
            {
                itemVM?.CancelEdit();
                return;
            }

            using (var context = new FlashcardDbContext())
            {
                var trackedDeck = await context.Decks.FindAsync(itemVM.Deck.Id);
                if (trackedDeck != null)
                {
                    trackedDeck.Name = itemVM.EditText;
                    await context.SaveChangesAsync();
                    
                    itemVM.Name = itemVM.EditText;
                    itemVM.IsEditing = false;
                }
                else
                {
                    itemVM.CancelEdit();
                }
            }
        }

        [RelayCommand]
        private void DeleteSubDeck(DeckItemViewModel? itemVM)
        {
            if (itemVM == null) return;
            _subDeckToConfirmDelete = itemVM;
            IsConfirmingDeleteSubDeck = true;
        }

        [RelayCommand]
        private async Task ConfirmDeleteSubDeck()
        {
            if (_subDeckToConfirmDelete == null) return;

            using (var context = new FlashcardDbContext())
            {
                var deckToDelete = await context.Decks.FindAsync(_subDeckToConfirmDelete.Deck.Id);
                if (deckToDelete != null)
                {
                    context.Decks.Remove(deckToDelete);
                    await context.SaveChangesAsync();
                }
            }

            SubDecks.Remove(_subDeckToConfirmDelete);
            _subDeckToConfirmDelete = null;
            IsConfirmingDeleteSubDeck = false;
            
            // Update total count
            if (_currentDeck != null)
            {
                await RefreshCardDataAsync();
            }
        }

        [RelayCommand]
        private void CancelDeleteSubDeck()
        {
            _subDeckToConfirmDelete = null;
            IsConfirmingDeleteSubDeck = false;
        }

        [RelayCommand]
        private async Task SaveCard()
        {
            if (_currentDeck == null || string.IsNullOrWhiteSpace(NewCardFront) || string.IsNullOrWhiteSpace(NewCardBack))
            {
                return;
            }

            using (var context = new FlashcardDbContext())
            {
                if (_cardToEdit != null)
                {
                    // FALL 1: WIR BEARBEITEN
                    var trackedCard = await context.Cards.FindAsync(_cardToEdit.Id);
                    if (trackedCard != null)
                    {
                        trackedCard.Front = NewCardFront;
                        trackedCard.Back = NewCardBack;
                        await context.SaveChangesAsync();
                        
                        // Speichere neue Bilder für die bearbeitete Karte
                        var pendingImages = GetPendingImages?.Invoke();
                        if (pendingImages != null && pendingImages.Count > 0)
                        {
                            foreach (var image in pendingImages)
                            {
                                image.CardId = trackedCard.Id;
                                context.CardImages.Add(image);
                            }
                            await context.SaveChangesAsync();
                        }
                    }
                    
                    OnNavigateToCardList?.Invoke(_currentDeck);
                    ResetToAddingMode();
                }
                else
                {
                    // FALL 2: WIR FÜGEN HINZU
                    int targetDeckId = _currentDeck.Id;

                    if (_currentDeck.ParentDeckId == null)
                    {
                        // 1. Suche nach markiertem Standard-Deck
                        var generalDeck = await context.Decks
                            .FirstOrDefaultAsync(d => d.ParentDeckId == _currentDeck.Id && d.IsDefault);

                        // 2. Fallback: Suche nach Namen "Allgemein" (für alte Daten)
                        if (generalDeck == null)
                        {
                            generalDeck = await context.Decks
                                .FirstOrDefaultAsync(d => d.ParentDeckId == _currentDeck.Id && d.Name == "Allgemein");
                            
                            // Wenn gefunden, markiere es als Default für die Zukunft
                            if (generalDeck != null)
                            {
                                generalDeck.IsDefault = true;
                                await context.SaveChangesAsync();
                            }
                        }
                        
                        // 3. Wenn immer noch nicht gefunden, erstelle neu
                        if (generalDeck == null)
                        {
                            generalDeck = new Deck 
                            { 
                                Name = "Allgemein", 
                                ParentDeckId = _currentDeck.Id,
                                IsDefault = true 
                            };
                            context.Decks.Add(generalDeck);
                            await context.SaveChangesAsync();
                        }
                        targetDeckId = generalDeck.Id;
                    }

                    var newCard = new Card
                    {
                        Front = NewCardFront,
                        Back = NewCardBack,
                        DeckId = targetDeckId
                    };
                    context.Cards.Add(newCard);
                    await context.SaveChangesAsync();
                    
                    // Speichere pending Bilder für die neue Karte
                    var pendingImages = GetPendingImages?.Invoke();
                    if (pendingImages != null && pendingImages.Count > 0)
                    {
                        foreach (var image in pendingImages)
                        {
                            image.CardId = newCard.Id;
                            context.CardImages.Add(image);
                        }
                        await context.SaveChangesAsync();
                    }
                    
                    await RefreshCardDataAsync(); 
                    RequestFrontFocus?.Invoke();
                }
            }

            if(_cardToEdit == null) 
            {
                NewCardFront = string.Empty;
                NewCardBack = string.Empty;
            }
        }
        
        [RelayCommand]
        private void CancelEdit()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
            ResetToAddingMode();
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_currentDeck?.ParentDeckId != null)
            {
                using (var context = new FlashcardDbContext())
                {
                    var parentDeck = context.Decks.Find(_currentDeck.ParentDeckId);
                    if (parentDeck != null)
                    {
                        OnNavigateToDeck?.Invoke(parentDeck);
                        return;
                    }
                }
            }
            OnNavigateBack?.Invoke();
        }
        
        [RelayCommand(CanExecute = nameof(HasCards))]
        private void GoToCardList()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
        }

        [RelayCommand]
        private void StartLearningAll()
        {
            if (_currentDeck != null)
            {
                OnNavigateToLearn?.Invoke(_currentDeck, LearningMode.AllRecursive, null);
            }
        }

        [RelayCommand]
        private void StartLearningMain()
        {
            if (_currentDeck != null)
            {
                // Find "Allgemein" subdeck (Default or by Name)
                var generalDeckVM = SubDecks.FirstOrDefault(d => d.Deck.IsDefault) 
                                    ?? SubDecks.FirstOrDefault(d => d.Name == "Allgemein");
                                    
                if (generalDeckVM != null)
                {
                    // Learn the "Allgemein" deck. 
                    OnNavigateToLearn?.Invoke(generalDeckVM.Deck, LearningMode.AllRecursive, null);
                }
                else
                {
                    // Fallback if no Allgemein deck exists (e.g. empty root deck)
                    OnNavigateToLearn?.Invoke(_currentDeck, LearningMode.MainOnly, null);
                }
            }
        }

        [RelayCommand]
        private async Task OpenSubDeckSelection()
        {
            if (_currentDeck == null) return;

            IsSubDeckSelectionVisible = true;
            SubDeckSelectionList.Clear();

            foreach (var subDeckVM in SubDecks)
            {
                SubDeckSelectionList.Add(new SubDeckSelectionItem(subDeckVM.Deck));
            }

            using (var context = new FlashcardDbContext())
            {
                var lastSession = await context.LearningSessions
                    .AsNoTracking()
                    .Where(s => s.DeckId == _currentDeck.Id && s.Mode == LearningMode.CustomSelection)
                    .OrderByDescending(s => s.LastAccessed)
                    .FirstOrDefaultAsync();

                if (lastSession != null && !string.IsNullOrEmpty(lastSession.SelectedDeckIdsJson))
                {
                    try 
                    {
                        var selectedIds = JsonSerializer.Deserialize<List<int>>(lastSession.SelectedDeckIdsJson);
                        if (selectedIds != null)
                        {
                            foreach (var item in SubDeckSelectionList)
                            {
                                item.IsSelected = selectedIds.Contains(item.Deck.Id);
                            }
                        }
                    }
                    catch { /* Ignore json errors */ }
                }
            }
        }        [RelayCommand]
        private void StartLearningCustom()
        {
            if (_currentDeck == null) return;

            var selectedIds = SubDeckSelectionList
                .Where(i => i.IsSelected)
                .Select(i => i.Deck.Id)
                .ToList();

            if (!selectedIds.Any()) return; // Or show error

            IsSubDeckSelectionVisible = false;
            OnNavigateToLearn?.Invoke(_currentDeck, LearningMode.CustomSelection, selectedIds);
        }

        [RelayCommand]
        private void CancelSubDeckSelection()
        {
            IsSubDeckSelectionVisible = false;
        }
        
        private void UpdateCardCount(int count)
        {
            CardCountText = $"Karteikarten anzeigen ({count})";
            HasCards = count > 0;

            if (_currentDeck != null)
            {
                OnCardCountUpdated?.Invoke(_currentDeck, count);
            }
        }

        private void ResetToAddingMode()
        {
            _cardToEdit = null;
            NewCardFront = string.Empty;
            NewCardBack = string.Empty;
            SaveButtonText = IsRootDeck ? "Allgemeine Karte hinzufügen" : "Karte zu Thema hinzufügen";
            IsEditing = false;
        }
    }
}