using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapyCard.ViewModels
{
    // Helper class for grouping cards
    public partial class CardGroupViewModel : ObservableObject
    {
        public string Title { get; }
        public ObservableCollection<CardItemViewModel> Cards { get; } = new();
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowSelectionIndicator))]
        private bool _isExpanded = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowSelectionIndicator))]
        private bool _hasSelection = false;

        public bool ShowSelectionIndicator => HasSelection && !IsExpanded;

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

        [ObservableProperty]
        private bool _isGridView = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewTopic))]
        private Card? _previewCard;

        public string PreviewTopic
        {
            get
            {
                if (PreviewCard == null) return string.Empty;
                var group = CardGroups.FirstOrDefault(g => g.Cards.Any(c => c.Card.Id == PreviewCard.Id));
                return group?.Title ?? string.Empty;
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        [NotifyPropertyChangedFor(nameof(CanNavigateNext))]
        [NotifyPropertyChangedFor(nameof(CanNavigatePrevious))]
        private bool _isPreviewOpen;

        [ObservableProperty]
        private bool _isConfirmingDelete;

        private Card? _cardToConfirmDelete;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEditButton))]
        private bool _isEditing = false;

        public bool CanNavigateNext => IsPreviewOpen && AllCards.ToList().FindIndex(c => c.Card.Id == PreviewCard?.Id) < AllCards.Count() - 1;
        public bool CanNavigatePrevious => IsPreviewOpen && AllCards.ToList().FindIndex(c => c.Card.Id == PreviewCard?.Id) > 0;

        [RelayCommand]
        private void NavigateNextPreview()
        {
            var cards = AllCards.ToList();
            int index = cards.FindIndex(c => c.Card.Id == PreviewCard?.Id);
            if (index >= 0 && index < cards.Count - 1)
            {
                PreviewCard = cards[index + 1].Card;
                OnPropertyChanged(nameof(CanNavigateNext));
                OnPropertyChanged(nameof(CanNavigatePrevious));
            }
        }

        [RelayCommand]
        private void NavigatePreviousPreview()
        {
            var cards = AllCards.ToList();
            int index = cards.FindIndex(c => c.Card.Id == PreviewCard?.Id);
            if (index > 0)
            {
                PreviewCard = cards[index - 1].Card;
                OnPropertyChanged(nameof(CanNavigateNext));
                OnPropertyChanged(nameof(CanNavigatePrevious));
            }
        }

        [ObservableProperty] private string _editFrontText = string.Empty;
        [ObservableProperty] private string _editBackText = string.Empty;

        [ObservableProperty] private bool _isImagePreviewOpen = false;
        [ObservableProperty] private object? _previewImageSource;
        private double _originalImageWidth;
        private double _originalImageHeight;
        private double _imageZoomLevel = 1.0;

        public double ImageZoomLevel
        {
            get => _imageZoomLevel;
            set
            {
                if (SetProperty(ref _imageZoomLevel, Math.Clamp(value, 0.1, 5.0)))
                {
                    OnPropertyChanged(nameof(ScaledImageWidth));
                    OnPropertyChanged(nameof(ScaledImageHeight));
                }
            }
        }

        public double ScaledImageWidth => _originalImageWidth * _imageZoomLevel;
        public double ScaledImageHeight => _originalImageHeight * _imageZoomLevel;

        [ObservableProperty] private double _defaultZoomLevel = 1.0;

        public bool ShowEditButton => IsPreviewOpen && !IsEditing;

        [RelayCommand]
        private void ToggleView() => IsGridView = !IsGridView;

        [RelayCommand]
        private void ClosePreview()
        {
            IsPreviewOpen = false;
            IsEditing = false;
        }

        [RelayCommand]
        private void StartEdit()
        {
            if (PreviewCard == null) return;
            IsEditing = true;
            EditFrontText = PreviewCard.Front;
            EditBackText = PreviewCard.Back;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
        }

        [RelayCommand]
        private async Task SaveEdit()
        {
            if (PreviewCard == null) return;

            using (var context = new FlashcardDbContext())
            {
                var card = await context.Cards.FindAsync(PreviewCard.Id);
                if (card != null)
                {
                    card.Front = EditFrontText;
                    card.Back = EditBackText;
                    await context.SaveChangesAsync();

                    // Update UI Models
                    var currentCard = PreviewCard;
                    currentCard.Front = EditFrontText;
                    currentCard.Back = EditBackText;
                    
                    // Trigger UI update by reassigning the property.
                    // Since Card is a POCO, we need a reference change or manual notification for child properties.
                    // Assigning a new reference or re-assigning the same one after nulling is a safe way.
                    PreviewCard = null;
                    PreviewCard = currentCard;

                    // Update the CardItemViewModel in the lists
                    var item = AllCards.FirstOrDefault(c => c.Card.Id == PreviewCard.Id);
                    if (item != null)
                    {
                        item.NotifyCardChanged();
                    }
                }
            }
            IsEditing = false;
        }

        [RelayCommand]
        private void OpenImagePreview(object imageSource)
        {
            if (imageSource is Bitmap bitmap)
            {
                _originalImageWidth = bitmap.Size.Width;
                _originalImageHeight = bitmap.Size.Height;
                OnPropertyChanged(nameof(ScaledImageWidth));
                OnPropertyChanged(nameof(ScaledImageHeight));
            }
            PreviewImageSource = imageSource;
            IsImagePreviewOpen = true;
        }

        [RelayCommand]
        private void CloseImagePreview()
        {
            IsImagePreviewOpen = false;
            PreviewImageSource = null;
            _originalImageWidth = 0;
            _originalImageHeight = 0;
            OnPropertyChanged(nameof(ScaledImageWidth));
            OnPropertyChanged(nameof(ScaledImageHeight));
        }

        [RelayCommand]
        private void ZoomIn() => ImageZoomLevel += 0.05;

        [RelayCommand]
        private void ZoomOut() => ImageZoomLevel -= 0.05;

        [RelayCommand]
        private void ResetZoom() => ImageZoomLevel = DefaultZoomLevel;

        public event Action? OnNavigateBack;
        public event Action<Card>? OnShowPreviewRequest;

        // NEU: Event für den "Speichern unter"-Dialog.
        // Input: string (vorgeschlagener Name), Output: Task<Stream?> (Stream zum Schreiben)
        public event Func<string, Task<Stream?>>? ShowSaveFileDialog;

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

        public event Action<Deck>? OnStartLearnRequest;

        [RelayCommand]
        private void StartLearn()
        {
            if (_currentDeck != null)
            {
                OnStartLearnRequest?.Invoke(_currentDeck);
            }
        }

        [RelayCommand]
        private void RequestDeleteCard(object? parameter)
        {
            if (parameter is CardItemViewModel itemVM)
            {
                _cardToConfirmDelete = itemVM.Card;
                IsConfirmingDelete = true;
            }
            else if (parameter is Card card)
            {
                _cardToConfirmDelete = card;
                IsConfirmingDelete = true;
            }
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsConfirmingDelete = false;
            _cardToConfirmDelete = null;
        }

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (_cardToConfirmDelete == null) return;

            using (var context = new FlashcardDbContext())
            {
                var cardToDelete = await context.Cards.FindAsync(_cardToConfirmDelete.Id);
                if (cardToDelete != null)
                {
                    context.Cards.Remove(cardToDelete);
                    await context.SaveChangesAsync();

                    // Handle navigation if preview is open
                    if (IsPreviewOpen && PreviewCard?.Id == _cardToConfirmDelete.Id)
                    {
                        var allCardsList = AllCards.ToList();
                        int index = allCardsList.FindIndex(c => c.Card.Id == _cardToConfirmDelete.Id);
                        
                        if (allCardsList.Count > 1)
                        {
                            // Move to next card, or previous if deleting the last one
                            int nextIndex = (index < allCardsList.Count - 1) ? index + 1 : index - 1;
                            PreviewCard = allCardsList[nextIndex].Card;
                        }
                        else
                        {
                            ClosePreview();
                        }
                    }

                    // UI updaten
                    var itemVM = AllCards.FirstOrDefault(c => c.Card.Id == _cardToConfirmDelete.Id);
                    if (itemVM != null)
                    {
                        var group = CardGroups.FirstOrDefault(g => g.Cards.Contains(itemVM));
                        group?.Cards.Remove(itemVM);
                    }
                    
                    UpdateSelectedCount();
                }
            }

            IsConfirmingDelete = false;
            _cardToConfirmDelete = null;
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
            if (itemVM != null)
            {
                PreviewCard = itemVM.Card;
                IsPreviewOpen = true;
                StartEdit();
            }
        }

        [RelayCommand]
        private void ShowPreview(CardItemViewModel? itemVM)
        {
            if (itemVM != null)
            {
                PreviewCard = itemVM.Card;
                IsPreviewOpen = true;
                OnShowPreviewRequest?.Invoke(itemVM.Card);
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
            string suggestedName = "Karten";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

            if (_currentDeck != null && activeGroup != null)
            {
                string subjectName; // "Fach"
                string topicName;   // "Thema"

                using (var context = new FlashcardDbContext())
                {
                    // Wenn das aktuelle Deck ein Parent hat, ist es ein Unterdeck (Thema).
                    // Das Parent ist das Fach.
                    if (_currentDeck.ParentDeckId != null)
                    {
                        var parent = await context.Decks.FindAsync(_currentDeck.ParentDeckId);
                        subjectName = parent?.Name ?? "Unbekannt";
                        // Bei Unterdecks ist der DeckName das Thema
                        topicName = _currentDeck.Name;
                    }
                    else
                    {
                        // Wir sind im Root-Deck (Fach).
                        subjectName = _currentDeck.Name;

                        // Thema ist entweder "Allgemein" oder der Name des Subdeck-Groups
                        if (activeGroup.Title == "Allgemein")
                        {
                            topicName = "Allgemein";
                        }
                        else
                        {
                            topicName = activeGroup.Title;
                        }
                    }
                }

                subjectName = SanitizeFileName(subjectName);
                topicName = SanitizeFileName(topicName);

                suggestedName = $"{subjectName}-{topicName}_{timestamp}";
            }
            else
            {
                suggestedName = $"Export-{timestamp}";
            }

            try
            {
                // 3. Den "Speichern unter"-Dialog aufrufen (wird von der View behandelt)
                // Dies liefert jetzt einen offenen Stream zurück
                using (var stream = await ShowSaveFileDialog.Invoke(suggestedName))
                {
                    // 4. Wenn der Nutzer einen Pfad ausgewählt hat (Stream ist nicht null)
                    if (stream != null)
                    {
                        // Console.WriteLine($"[PDF] Stream opened. CanWrite: {stream.CanWrite}");
                        
                        try 
                        {
                            // 5. PDF generieren und in den Stream schreiben
                            PdfGenerationService.GeneratePdf(stream, selectedCards, SelectedColumnCount);
                            // Console.WriteLine("[PDF] Generation successful.");
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"[PDF] Generation FAILED: {innerEx.ToString()}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[PDF] Stream was null (User cancelled?)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PDF] Outer ERROR: {ex.Message}");
                Console.WriteLine($"[PDF] StackTrace: {ex.StackTrace}");
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
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
            
            // Update HasSelection for each group
            foreach (var group in CardGroups)
            {
                group.HasSelection = group.Cards.Any(c => c.IsSelected);
            }
        }
    }
}