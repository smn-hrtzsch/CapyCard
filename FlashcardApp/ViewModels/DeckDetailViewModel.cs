using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashcardApp.Data;
using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace FlashcardApp.ViewModels
{
    public partial class DeckDetailViewModel : ObservableObject
    {
        private readonly FlashcardDbContext _dbContext;
        private Deck? _currentDeck;
        private Card? _cardToEdit;

        [ObservableProperty]
        private string _newCardFront = string.Empty;

        [ObservableProperty]
        private string _newCardBack = string.Empty;

        [ObservableProperty]
        private string _deckName = "Fach laden...";

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

        public ObservableCollection<Card> Cards { get; } = new();
        public ObservableCollection<Deck> SubDecks { get; } = new();
        public ObservableCollection<SubDeckSelectionItem> SubDeckSelectionList { get; } = new();

        public event Action? OnNavigateBack;
        public event Action<Deck>? OnNavigateToCardList; 
        public event Action<Deck, LearningMode, List<int>?>? OnNavigateToLearn; // Updated signature
        public event Action<Deck>? OnNavigateToDeck; // New event for subdeck navigation
        public event Action<Deck, int>? OnCardCountUpdated;
        public event Action? OnSubDeckAdded; // New event for subdeck addition
        public event Action? RequestFrontFocus;

        public DeckDetailViewModel()
        {
            _dbContext = new FlashcardDbContext();
        }

        public async Task LoadDeck(Deck deck)
        {
            _currentDeck = deck;
            DeckName = deck.Name;
            ResetToAddingMode(); 
            await RefreshCardDataAsync();
        }
        
        public async Task LoadCardForEditing(Deck deck, Card card)
        {
            _currentDeck = deck;
            _cardToEdit = card;
            DeckName = deck.Name;

            NewCardFront = card.Front;
            NewCardBack = card.Back;
            
            SaveButtonText = "Änderungen speichern";
            IsEditing = true;
            
            await RefreshCardDataAsync();
        }

        public async Task RefreshCardDataAsync()
        {
            if (_currentDeck == null) return;
            
            // --- HIER IST DIE KORREKTUR ---
            // 'AsNoTracking()' zwingt EF Core, die Datenbank-Datei
            // neu abzufragen (z.B. nach einem Löschvorgang im CardListViewModel)
            // und nicht die veralteten Daten aus seinem eigenen Cache zu verwenden.
            var deckFromDb = await _dbContext.Decks
                                 .AsNoTracking() 
                                 .Include(d => d.Cards)
                                 .Include(d => d.SubDecks)
                                 .ThenInclude(sd => sd.Cards) // Include cards of subdecks for count
                                 .FirstOrDefaultAsync(d => d.Id == _currentDeck.Id);
            
            // Wir müssen _currentDeck aktualisieren, falls es null war, 
            // aber hauptsächlich brauchen wir die Kartenliste.
            if (deckFromDb == null) 
            {
                // Das Deck selbst wurde gelöscht, gehe zurück
                GoBack();
                return; 
            }
            
            _currentDeck = deckFromDb; 
            CanAddSubDecks = _currentDeck.ParentDeckId == null;
            IsRootDeck = _currentDeck.ParentDeckId == null;
            
            // Update button text based on deck type if not editing
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
            // Sort by ID to keep insertion order (newest at bottom), but "Allgemein" always on top
            var sortedSubDecks = deckFromDb.SubDecks
                .OrderByDescending(d => d.Name == "Allgemein") // true comes first
                .ThenBy(d => d.Id);

            foreach (var subDeck in sortedSubDecks)
            {
                SubDecks.Add(subDeck);
            }
            
            // Calculate total cards (recursive)
            int totalCards = Cards.Count + SubDecks.Sum(sd => sd.Cards.Count);
            UpdateCardCount(totalCards);
        }
        
        [RelayCommand]
        private async Task AddSubDeck()
        {
            // Prevent nesting: Only allow subdecks if current deck is a root deck (ParentDeckId is null)
            if (string.IsNullOrWhiteSpace(NewSubDeckName) || _currentDeck == null || _currentDeck.ParentDeckId != null) return;

            var newDeck = new Deck
            {
                Name = NewSubDeckName,
                ParentDeckId = _currentDeck.Id
            };
            _dbContext.Decks.Add(newDeck);
            await _dbContext.SaveChangesAsync();
            
            NewSubDeckName = string.Empty;
            await RefreshCardDataAsync();
            OnSubDeckAdded?.Invoke();
        }

        [RelayCommand]
        private void OpenSubDeck(Deck subDeck)
        {
            OnNavigateToDeck?.Invoke(subDeck);
        }

        [RelayCommand]
        private async Task SaveCard()
        {
            if (_currentDeck == null || string.IsNullOrWhiteSpace(NewCardFront) || string.IsNullOrWhiteSpace(NewCardBack))
            {
                return;
            }

            if (_cardToEdit != null)
            {
                // FALL 1: WIR BEARBEITEN
                var trackedCard = _dbContext.Cards.Find(_cardToEdit.Id);
                if (trackedCard != null)
                {
                    trackedCard.Front = NewCardFront;
                    trackedCard.Back = NewCardBack;
                }
                else
                {
                    _cardToEdit.Front = NewCardFront; 
                    _cardToEdit.Back = NewCardBack;
                    _dbContext.Entry(_cardToEdit).State = EntityState.Modified;
                }
                await _dbContext.SaveChangesAsync();
                
                OnNavigateToCardList?.Invoke(_currentDeck);
                ResetToAddingMode();
            }
            else
            {
                // FALL 2: WIR FÜGEN HINZU
                int targetDeckId = _currentDeck.Id;

                // If this is a root deck, add to "Allgemein" subdeck instead
                if (_currentDeck.ParentDeckId == null)
                {
                    var generalDeck = await _dbContext.Decks
                        .FirstOrDefaultAsync(d => d.ParentDeckId == _currentDeck.Id && d.Name == "Allgemein");
                    
                    if (generalDeck == null)
                    {
                        generalDeck = new Deck { Name = "Allgemein", ParentDeckId = _currentDeck.Id };
                        _dbContext.Decks.Add(generalDeck);
                        await _dbContext.SaveChangesAsync();
                    }
                    targetDeckId = generalDeck.Id;
                }

                var newCard = new Card
                {
                    Front = NewCardFront,
                    Back = NewCardBack,
                    DeckId = targetDeckId
                };
                _dbContext.Cards.Add(newCard);
                await _dbContext.SaveChangesAsync();
                await RefreshCardDataAsync(); 
                RequestFrontFocus?.Invoke();
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
                var parentDeck = _dbContext.Decks.Find(_currentDeck.ParentDeckId);
                if (parentDeck != null)
                {
                    OnNavigateToDeck?.Invoke(parentDeck);
                    return;
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
                // Find "Allgemein" subdeck
                var generalDeck = SubDecks.FirstOrDefault(d => d.Name == "Allgemein");
                if (generalDeck != null)
                {
                    // Learn the "Allgemein" deck. 
                    OnNavigateToLearn?.Invoke(generalDeck, LearningMode.AllRecursive, null);
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

            // Add Main Deck as an option too? User said "inklusive des Hauptfaches"
            // UPDATE: User requested "Hauptfach keine Auswahlmöglichkeit mehr"
            // SubDeckSelectionList.Add(new SubDeckSelectionItem(_currentDeck) { IsSelected = true });

            foreach (var subDeck in SubDecks)
            {
                SubDeckSelectionList.Add(new SubDeckSelectionItem(subDeck));
            }

            // Try to load previous selection from DB
            // We need to find the last session for this deck with CustomSelection mode
            // Use AsNoTracking to ensure we get the latest data from DB, not stale cache
            var lastSession = await _dbContext.LearningSessions
                .AsNoTracking()
                .Where(s => s.DeckId == _currentDeck.Id && s.Mode == LearningMode.CustomSelection)
                .OrderByDescending(s => s.LastAccessed) // Use LastAccessed instead of Id
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

        [RelayCommand]
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