using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Platform.Storage;

using CapyCard.Services;

namespace CapyCard.ViewModels
{
    public partial class DeckDetailViewModel : ObservableObject
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly ICardDraftService _cardDraftService;
        private Deck? _currentDeck;
        private Card? _cardToEdit;
        private int _loadSequence;
        private bool _suppressDraftSave;
        private int? _draftDeckId;
        private string _draftFront = string.Empty;
        private string _draftBack = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDraft))]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDraft))]
        private string _newCardBack = string.Empty;

        public bool HasDraft => !string.IsNullOrWhiteSpace(NewCardFront) || !string.IsNullOrWhiteSpace(NewCardBack);

        [ObservableProperty]
        private string _deckName = "Fach laden...";

        [ObservableProperty]
        private bool _isLoading;

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
        private bool _isSubDeckListOpen;

        [RelayCommand]
        private void ToggleSubDeckList()
        {
            IsSubDeckListOpen = !IsSubDeckListOpen;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddSubDeckCommand))]
        private string _newSubDeckName = string.Empty;

        [ObservableProperty]
        private bool _canAddSubDecks = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartLearningCustomCommand))]
        private bool _isSubDeckSelectionVisible = false;

        [ObservableProperty]
        private bool _isRootDeck = false;

        [ObservableProperty]
        private bool _isConfirmingDeleteSubDeck = false;

        private DeckItemViewModel? _subDeckToConfirmDelete;

        public ObservableCollection<Card> Cards { get; } = new();
        public ObservableCollection<DeckItemViewModel> SubDecks { get; } = new();
        public ObservableCollection<SubDeckSelectionItem> SubDeckSelectionList { get; } = new();
        
        // Export ViewModel
        public ExportViewModel ExportViewModel { get; }

        public event Action? OnNavigateBack;
        public event Action<Deck>? OnNavigateToCardList; 
        public event Action<Deck, LearningMode, List<int>?>? OnNavigateToLearn; // Updated signature
        public event Action<Deck>? OnNavigateToDeck; // New event for subdeck navigation
        public event Action<Deck, int>? OnCardCountUpdated;
        public event Action? OnSubDeckAdded; // New event for subdeck addition
        public event Action? RequestFrontFocus;
        
        // Event for file picker (to be wired up from View)
        public event Func<string, string, Task<IStorageFile?>>? OnRequestFileSave;

        [ObservableProperty]
        private bool _isEditorToolbarVisible = true;

        public DeckDetailViewModel(IUserSettingsService userSettingsService, ICardDraftService cardDraftService)
        {
            _userSettingsService = userSettingsService;
            _cardDraftService = cardDraftService;
            // _dbContext removed. We use short-lived contexts now.
            ExportViewModel = new ExportViewModel();
            
            // Wire up export events
            ExportViewModel.OnRequestFileSave += async (name, ext) => 
                await (OnRequestFileSave?.Invoke(name, ext) ?? Task.FromResult<IStorageFile?>(null));
        }

        public DeckDetailViewModel() : this(new UserSettingsService(), new CardDraftService()) { }

        partial void OnNewCardFrontChanged(string value)
        {
            _ = SaveDraftIfNeededAsync();
        }

        partial void OnNewCardBackChanged(string value)
        {
            _ = SaveDraftIfNeededAsync();
        }

        private async void LoadToolbarSettings()
        {
             var settings = await _userSettingsService.LoadSettingsAsync();
             IsEditorToolbarVisible = settings.ShowEditorToolbar;
        }

        public async Task LoadDeckAsync(Deck deck)
        {
            int loadId = ++_loadSequence;
            IsLoading = true;

            try
            {
                LoadToolbarSettings();
                _currentDeck = deck;
                _cardToEdit = null;

                IsEditing = false;
                IsRootDeck = deck.ParentDeckId == null;
                CanAddSubDecks = IsRootDeck;
                IsSubDeckSelectionVisible = false;
                IsConfirmingDeleteSubDeck = false;
                IsSubDeckListOpen = false;
                IsSubDeckSelectionVisible = false;
                IsConfirmingDeleteSubDeck = false;
                IsSubDeckListOpen = false;

                DeckName = deck.Name;
                BackButtonText = deck.ParentDeckId != null ? "Zurück zum Hauptfach" : "Zurück zur Fächerliste";

                Cards.Clear();
                SubDecks.Clear();
                UpdateCardCount(0);

                var headerTask = UpdateDeckHeaderAsync(loadId, deck);
                var draftTask = LoadDraftForCurrentDeckAsync();
                var dataTask = RefreshCardDataAsync(loadId);

                await Task.WhenAll(headerTask, draftTask, dataTask);

                if (loadId != _loadSequence)
                {
                    return;
                }

                ResetToAddingMode(restoreDraft: true);
            }
            finally
            {
                if (loadId == _loadSequence)
                {
                    IsLoading = false;
                }
            }
        }

        public Task LoadDeck(Deck deck) => LoadDeckAsync(deck);

        private async Task UpdateDeckHeaderAsync(int loadId, Deck deck)
        {
            if (deck.ParentDeckId == null)
            {
                return;
            }

            using (var context = new FlashcardDbContext())
            {
                var parentName = await context.Decks
                    .AsNoTracking()
                    .Where(d => d.Id == deck.ParentDeckId)
                    .Select(d => d.Name)
                    .FirstOrDefaultAsync();

                if (loadId != _loadSequence)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    DeckName = $"{parentName} > {deck.Name}";
                }
            }
        }
        
        public async Task LoadCardForEditingAsync(Deck deck, Card card)
        {
            int loadId = ++_loadSequence;
            IsLoading = true;

            try
            {
                LoadToolbarSettings();
                _currentDeck = deck;
                _cardToEdit = card;

                await SaveDraftIfNeededAsync();

                IsRootDeck = deck.ParentDeckId == null;
                CanAddSubDecks = IsRootDeck;

                DeckName = deck.Name;
                BackButtonText = deck.ParentDeckId != null ? "Zurück zum Hauptfach" : "Zurück zur Fächerliste";

                var headerTask = UpdateDeckHeaderAsync(loadId, deck);
                var dataTask = RefreshCardDataAsync(loadId);

                SetEditorValues(card.Front, card.Back);

                SaveButtonText = "Änderungen speichern";
                IsEditing = true;

                await Task.WhenAll(headerTask, dataTask);
            }
            finally
            {
                if (loadId == _loadSequence)
                {
                    IsLoading = false;
                }
            }
        }

        public Task LoadCardForEditing(Deck deck, Card card) => LoadCardForEditingAsync(deck, card);

        public async Task RefreshCardDataAsync(int? loadIdOverride = null)
        {
            if (_currentDeck == null) return;

            int loadId = loadIdOverride ?? _loadSequence;

            using (var context = new FlashcardDbContext())
            {
                var subDecks = await context.Decks
                    .AsNoTracking()
                    .Where(d => d.ParentDeckId == _currentDeck.Id)
                    .OrderByDescending(d => d.IsDefault)
                    .ThenByDescending(d => d.Name == "Allgemein")
                    .ThenBy(d => d.Id)
                    .Select(d => new { d.Id, d.Name, d.IsDefault })
                    .ToListAsync();

                if (loadId != _loadSequence)
                {
                    return;
                }

                var deckIds = subDecks.Select(d => d.Id).Append(_currentDeck.Id).ToList();

                var cardCounts = await context.Cards
                    .AsNoTracking()
                    .Where(c => deckIds.Contains(c.DeckId))
                    .GroupBy(c => c.DeckId)
                    .Select(g => new { DeckId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.DeckId, g => g.Count);

                if (loadId != _loadSequence)
                {
                    return;
                }

                if (!IsEditing)
                {
                    SaveButtonText = IsRootDeck ? "Allgemeine Karte hinzufügen" : "Karte zu Thema hinzufügen";
                }

                Cards.Clear();
                SubDecks.Clear();

                foreach (var subDeck in subDecks)
                {
                    var deckModel = new Deck
                    {
                        Id = subDeck.Id,
                        Name = subDeck.Name,
                        ParentDeckId = _currentDeck.Id,
                        IsDefault = subDeck.IsDefault
                    };

                    var subDeckCount = cardCounts.TryGetValue(subDeck.Id, out var count) ? count : 0;
                    var vm = new DeckItemViewModel(deckModel, subDeckCount);

                    if (subDeck.IsDefault || subDeck.Name == "Allgemein")
                    {
                        vm.IsStatic = true;
                    }

                    SubDecks.Add(vm);
                }

                int totalCards = 0;
                if (cardCounts.TryGetValue(_currentDeck.Id, out var ownCount))
                {
                    totalCards += ownCount;
                }

                foreach (var subDeck in subDecks)
                {
                    if (cardCounts.TryGetValue(subDeck.Id, out var count))
                    {
                        totalCards += count;
                    }
                }

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
                    }
                    
                    OnNavigateToCardList?.Invoke(_currentDeck);
                    ResetToAddingMode(restoreDraft: true);
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
                    await RefreshCardDataAsync(); 
                    await ClearDraftAsync();
                    ResetToAddingMode(restoreDraft: false);
                    RequestFrontFocus?.Invoke();
                }
            }
        }
        
        [RelayCommand]
        private void CancelEdit()
        {
            if (_currentDeck != null)
            {
                OnNavigateToCardList?.Invoke(_currentDeck);
            }
            ResetToAddingMode(restoreDraft: true);
        }

        [RelayCommand]
        private async Task DiscardDraft()
        {
            await ClearDraftAsync();
            ResetToAddingMode(restoreDraft: false);
            RequestFrontFocus?.Invoke();
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

            SubDeckSelectionList.Clear();

            foreach (var subDeckVM in SubDecks)
            {
                SubDeckSelectionList.Add(new SubDeckSelectionItem(subDeckVM.Deck));
            }

            IsSubDeckSelectionVisible = true;

            try
            {
                using (var context = new FlashcardDbContext())
                {
                    var lastSession = await context.LearningSessions
                        .AsNoTracking()
                        .Where(s => s.DeckId == _currentDeck.Id && s.Scope == LearningMode.CustomSelection)
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load previous selection: {ex.Message}");
            }
        }
        
        [RelayCommand(CanExecute = nameof(IsSubDeckSelectionVisible))]
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

        [RelayCommand]
        private void HandleEscape()
        {
            // Priority: Close dialogs first, then dropdown
            if (IsSubDeckSelectionVisible)
            {
                IsSubDeckSelectionVisible = false;
            }
            else if (IsConfirmingDeleteSubDeck)
            {
                IsConfirmingDeleteSubDeck = false;
                _subDeckToConfirmDelete = null;
            }
            else if (IsSubDeckListOpen)
            {
                IsSubDeckListOpen = false;
            }
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

        private void SetEditorValues(string front, string back)
        {
            _suppressDraftSave = true;
            try
            {
                NewCardFront = front;
                NewCardBack = back;
            }
            finally
            {
                _suppressDraftSave = false;
            }
        }

        private async Task LoadDraftForCurrentDeckAsync()
        {
            if (_currentDeck == null)
            {
                return;
            }

            var draft = await _cardDraftService.LoadDraftAsync(_currentDeck.Id);
            _draftDeckId = _currentDeck.Id;
            _draftFront = draft?.Front ?? string.Empty;
            _draftBack = draft?.Back ?? string.Empty;
        }

        private async Task SaveDraftIfNeededAsync()
        {
            if (_suppressDraftSave || IsEditing || _currentDeck == null)
            {
                return;
            }

            _draftDeckId = _currentDeck.Id;
            _draftFront = NewCardFront;
            _draftBack = NewCardBack;

            await _cardDraftService.SaveDraftAsync(_currentDeck.Id, _draftFront, _draftBack);
        }

        private async Task ClearDraftAsync()
        {
            if (_currentDeck == null)
            {
                return;
            }

            _draftDeckId = _currentDeck.Id;
            _draftFront = string.Empty;
            _draftBack = string.Empty;

            await _cardDraftService.ClearDraftAsync(_currentDeck.Id);
        }

        private void RestoreDraftToEditors()
        {
            if (_currentDeck == null || _draftDeckId != _currentDeck.Id)
            {
                SetEditorValues(string.Empty, string.Empty);
                return;
            }

            SetEditorValues(_draftFront, _draftBack);
        }

        private void ResetToAddingMode(bool restoreDraft)
        {
            _cardToEdit = null;
            SaveButtonText = IsRootDeck ? "Allgemeine Karte hinzufügen" : "Karte zu Thema hinzufügen";
            IsEditing = false;

            if (restoreDraft)
            {
                RestoreDraftToEditors();
            }
            else
            {
                SetEditorValues(string.Empty, string.Empty);
            }
        }

        [RelayCommand]
        private async Task Export()
        {
            if (_currentDeck != null)
            {
                await ExportViewModel.ShowAsync(_currentDeck);
            }
        }
    }
}
