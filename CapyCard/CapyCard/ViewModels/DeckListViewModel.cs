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
using Avalonia.Platform.Storage;
using CapyCard.Services;

namespace CapyCard.ViewModels
{
    public partial class DeckListViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _newDeckName = string.Empty;

        [ObservableProperty]
        private bool _isConfirmingDelete = false;

        private DeckItemViewModel? _deckToConfirmDelete;

        public ObservableCollection<DeckItemViewModel> Decks { get; } = new();

        public ImportViewModel ImportViewModel { get; }
        public FormatInfoViewModel FormatInfoViewModel { get; }
        public ImportHelpViewModel ImportHelpViewModel { get; }
        public LegalViewModel LegalViewModel { get; }
        
        public event Func<Task<IStorageFile?>>? OnRequestFileOpen;
        
        private DeckItemViewModel? _selectedDeck;
        public DeckItemViewModel? SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                if (SetProperty(ref _selectedDeck, value) && value != null)
                {
                    if (!value.IsEditing)
                    {
                        OnDeckSelected?.Invoke(value.Deck);
                        _selectedDeck = null;
                        OnPropertyChanged(nameof(SelectedDeck));
                    }
                }
            }
        }
        public event Action<Deck>? OnDeckSelected;

        public SettingsViewModel SettingsViewModel { get; }

        public DeckListViewModel(IUserSettingsService userSettingsService, ThemeService themeService)
        {
            ImportViewModel = new ImportViewModel();
            FormatInfoViewModel = new FormatInfoViewModel();
            ImportHelpViewModel = new ImportHelpViewModel();
            LegalViewModel = new LegalViewModel();
            SettingsViewModel = new SettingsViewModel(userSettingsService, themeService);

            ImportViewModel.OnRequestFileOpen += async () => await (OnRequestFileOpen?.Invoke() ?? Task.FromResult<IStorageFile?>(null));
            ImportViewModel.OnImportCompleted += OnImportCompleted;
            ImportViewModel.RequestShowFormatInfo += () => ImportHelpViewModel.Show();
            _ = LoadDecksAsync();
        }
        
        public DeckListViewModel() : this(new UserSettingsService(), new ThemeService()) 
        {
        }

        [RelayCommand]
        private void OpenLegal()
        {
            LegalViewModel.Show();
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            await SettingsViewModel.InitializeAsync();
            SettingsViewModel.IsOpen = true;
        }

        private void OnImportCompleted(ImportResult result)
        {
            _ = LoadDecksAsync();
        }

        public void RefreshDecks()
        {
            _ = LoadDecksAsync();
        }

        private async Task LoadDecksAsync()
        {
            Decks.Clear();
            
            using (var context = new FlashcardDbContext())
            {
                var allDecks = await context.Decks
                    .AsNoTracking()
                    .ToListAsync();

                var cardCountByDeckId = await context.Cards
                    .AsNoTracking()
                    .GroupBy(c => c.DeckId)
                    .Select(g => new { DeckId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.DeckId, g => g.Count);

                var rootDecks = allDecks
                    .Where(d => d.ParentDeckId == null)
                    .ToList();

                var decksByParentId = allDecks
                    .Where(d => d.ParentDeckId != null)
                    .GroupBy(d => d.ParentDeckId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var totalCardCountByDeckId = new Dictionary<int, int>();

                int GetTotalCards(Deck deck)
                {
                    if (totalCardCountByDeckId.TryGetValue(deck.Id, out var total))
                    {
                        return total;
                    }

                    total = cardCountByDeckId.TryGetValue(deck.Id, out var ownCount) ? ownCount : 0;
                    if (decksByParentId.TryGetValue(deck.Id, out var children))
                    {
                        foreach (var child in children)
                        {
                            total += GetTotalCards(child);
                        }
                    }

                    totalCardCountByDeckId[deck.Id] = total;
                    return total;
                }

                foreach (var deck in rootDecks)
                {
                    var vm = CreateDeckItemViewModel(deck, decksByParentId, GetTotalCards);
                    Decks.Add(vm);
                }
            }
        }

        private DeckItemViewModel CreateDeckItemViewModel(
            Deck deck,
            IReadOnlyDictionary<int, List<Deck>> decksByParentId,
            Func<Deck, int> totalCardCountProvider)
        {
            int totalCards = totalCardCountProvider(deck);
            var vm = new DeckItemViewModel(deck, totalCards);
            
            if (decksByParentId.TryGetValue(deck.Id, out var subDecks) && subDecks.Count > 0)
            {
                vm.HasSubDecks = true;
                foreach (var subDeck in subDecks
                    .OrderByDescending(d => d.Name == "Allgemein")
                    .ThenBy(d => d.Id))
                {
                    vm.SubDecks.Add(CreateDeckItemViewModel(subDeck, decksByParentId, totalCardCountProvider));
                }
            }
            
            return vm;
        }

        public void UpdateDeckCardCount(int deckId, int cardCount)
        {
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
            
            Decks.Add(new DeckItemViewModel(newDeck));
            NewDeckName = string.Empty;
        }

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
                var deckToDelete = await context.Decks.FindAsync(_deckToConfirmDelete.Deck.Id);
                if (deckToDelete != null)
                {
                    context.Decks.Remove(deckToDelete);
                    await context.SaveChangesAsync();
                }
            }

            if (!RemoveRecursive(Decks, _deckToConfirmDelete))
            {
                Decks.Remove(_deckToConfirmDelete);
            }

            _deckToConfirmDelete = null;
            IsConfirmingDelete = false;
        }

        private bool RemoveRecursive(ObservableCollection<DeckItemViewModel> list, DeckItemViewModel itemToRemove)
        {
            if (list.Remove(itemToRemove)) return true;
            foreach (var item in list)
            {
                if (RemoveRecursive(item.SubDecks, itemToRemove)) return true;
            }
            return false;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            _deckToConfirmDelete = null;
            IsConfirmingDelete = false; 
        }

        [RelayCommand]
        private async Task SaveDeckEdit(DeckItemViewModel? itemVM)
        {
            if (itemVM == null || string.IsNullOrWhiteSpace(itemVM.EditText))
            {
                itemVM?.CancelEdit();
                SelectedDeck = null;
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

            SelectedDeck = null;
        }

        [RelayCommand]
        private void OpenDeck(DeckItemViewModel? deckVM)
        {
            if (deckVM != null && !deckVM.IsEditing)
            {
                OnDeckSelected?.Invoke(deckVM.Deck);
            }
        }

        [RelayCommand]
        private void SelectSubDeck(DeckItemViewModel? subDeckVM)
        {
            if (subDeckVM != null)
            {
                OnDeckSelected?.Invoke(subDeckVM.Deck);
            }
        }

        [RelayCommand]
        private async Task Import()
        {
            await ImportViewModel.ShowAsync();
        }

        [RelayCommand]
        private void ShowFormatInfo()
        {
            FormatInfoViewModel.Show();
        }
    }
}
