using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.ViewModels
{
    /// <summary>
    /// Item für die SubDeck-Auswahl beim Export.
    /// </summary>
    public partial class ExportSubDeckItem : ObservableObject
    {
        public Deck Deck { get; }
        public string Name => Deck.Name;
        public int CardCount { get; }

        [ObservableProperty]
        private bool _isSelected;

        public ExportSubDeckItem(Deck deck, int cardCount)
        {
            Deck = deck;
            CardCount = cardCount;
        }
    }

    public partial class ExportViewModel : ObservableObject
    {
        private readonly IImportExportService _importExportService;
        private Deck? _currentDeck;
        private List<int>? _selectedCardIds;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private bool _isExporting;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        // Deck Info
        [ObservableProperty]
        private string _deckName = string.Empty;

        [ObservableProperty]
        private string _totalCardCount = string.Empty;

        // Format Selection
        [ObservableProperty]
        private bool _formatCapyCardSelected = true;

        [ObservableProperty]
        private bool _formatAnkiSelected;

        [ObservableProperty]
        private bool _formatCsvSelected;

        [ObservableProperty]
        private bool _isAnkiAvailable = true;

        // Scope Selection
        [ObservableProperty]
        private bool _scopeFullDeckSelected = true;

        [ObservableProperty]
        private bool _scopeSelectedSubDecksSelected;

        [ObservableProperty]
        private bool _scopeSelectedCardsSelected;

        [ObservableProperty]
        private bool _hasSelectedCards;

        [ObservableProperty]
        private string _selectedCardsText = string.Empty;

        // SubDecks for selection
        public ObservableCollection<ExportSubDeckItem> SubDecks { get; } = new();

        // Options
        [ObservableProperty]
        private bool _includeProgress;

        public event Func<string, string, Task<IStorageFile?>>? OnRequestFileSave;
        public event Action<ExportResult>? OnExportCompleted;

        public ExportViewModel()
        {
            _importExportService = new ImportExportService();

#if BROWSER
            IsAnkiAvailable = false;
#endif
        }

        public async Task ShowAsync(Deck deck, List<int>? selectedCardIds = null)
        {
            _currentDeck = deck;
            _selectedCardIds = selectedCardIds;

            IsVisible = true;
            HasError = false;
            ErrorMessage = string.Empty;
            FormatCapyCardSelected = true;
            FormatAnkiSelected = false;
            FormatCsvSelected = false;
            ScopeFullDeckSelected = true;
            ScopeSelectedSubDecksSelected = false;
            ScopeSelectedCardsSelected = false;
            IncludeProgress = false;

            DeckName = deck.Name;
            HasSelectedCards = selectedCardIds?.Count > 0;
            SelectedCardsText = HasSelectedCards ? $"Nur ausgewählte Karten ({selectedCardIds!.Count})" : "";

            await LoadDeckDataAsync();
        }

        private async Task LoadDeckDataAsync()
        {
            if (_currentDeck == null) return;

            SubDecks.Clear();

            using var context = new FlashcardDbContext();
            var deckWithData = await context.Decks
                .AsNoTracking()
                .Include(d => d.SubDecks)
                    .ThenInclude(sd => sd.Cards)
                .Include(d => d.Cards)
                .FirstOrDefaultAsync(d => d.Id == _currentDeck.Id);

            if (deckWithData == null) return;

            int totalCards = deckWithData.Cards.Count;

            foreach (var subDeck in deckWithData.SubDecks.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name))
            {
                SubDecks.Add(new ExportSubDeckItem(subDeck, subDeck.Cards.Count));
                totalCards += subDeck.Cards.Count;
            }

            TotalCardCount = $"{totalCards} Karten";
        }

        [RelayCommand]
        private async Task Export()
        {
            if (_currentDeck == null)
            {
                HasError = true;
                ErrorMessage = "Kein Fach zum Exportieren ausgewählt.";
                return;
            }

            if (OnRequestFileSave == null) return;

            var format = GetSelectedFormat();
            var extension = format switch
            {
                ExportFormat.CapyCard => ".capycard",
                ExportFormat.Anki => ".apkg",
                ExportFormat.Csv => ".csv",
                _ => ".capycard"
            };

            var suggestedName = BuildSuggestedFileName(extension);
            var file = await OnRequestFileSave.Invoke(suggestedName, extension);
            if (file == null) return;

            HasError = false;
            IsExporting = true;
            StatusMessage = "Export läuft...";

            try
            {
                var options = new ExportOptions
                {
                    DeckId = _currentDeck.Id,
                    Format = format,
                    Scope = GetSelectedScope(),
                    IncludeProgress = IncludeProgress,
                    SelectedSubDeckIds = ScopeSelectedSubDecksSelected
                        ? SubDecks.Where(s => s.IsSelected).Select(s => s.Deck.Id).ToList()
                        : null,
                    SelectedCardIds = ScopeSelectedCardsSelected ? _selectedCardIds : null
                };

                await using var stream = await file.OpenWriteAsync();
                var result = await _importExportService.ExportAsync(stream, options);

                if (result.Success)
                {
                    IsVisible = false;
                    OnExportCompleted?.Invoke(result);
                }
                else
                {
                    HasError = true;
                    ErrorMessage = result.ErrorMessage ?? "Export fehlgeschlagen.";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Fehler beim Export: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private void HandleEscape()
        {
            Cancel();
        }

        [RelayCommand]
        private void HandleEnter()
        {
            if (ExportCommand.CanExecute(null))
                ExportCommand.Execute(null);
        }

        [RelayCommand]
        private void Cancel()
        {
            IsVisible = false;
        }

        private ExportFormat GetSelectedFormat()
        {
            if (FormatAnkiSelected) return ExportFormat.Anki;
            if (FormatCsvSelected) return ExportFormat.Csv;
            return ExportFormat.CapyCard;
        }

        private ExportScope GetSelectedScope()
        {
            if (ScopeSelectedSubDecksSelected) return ExportScope.SelectedSubDecks;
            if (ScopeSelectedCardsSelected) return ExportScope.SelectedCards;
            return ExportScope.FullDeck;
        }

        private string BuildSuggestedFileName(string extension)
        {
            if (_currentDeck == null)
                return $"Export{extension}";

            var deckName = SanitizeFileName(_currentDeck.Name);
            if (string.IsNullOrWhiteSpace(deckName))
                deckName = "Export";

            if (ScopeSelectedSubDecksSelected)
            {
                var selectedNames = SubDecks
                    .Where(s => s.IsSelected)
                    .Select(s => SanitizeFileName(s.Name))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (selectedNames.Count == 1)
                    return $"{selectedNames[0]}{extension}";

                if (selectedNames.Count > 1)
                    return $"{deckName}-{string.Join("-", selectedNames)}{extension}";
            }

            return $"{deckName}{extension}";
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        partial void OnFormatCapyCardSelectedChanged(bool value)
        {
            if (value)
            {
                FormatAnkiSelected = false;
                FormatCsvSelected = false;
            }
        }

        partial void OnFormatAnkiSelectedChanged(bool value)
        {
            if (value)
            {
                FormatCapyCardSelected = false;
                FormatCsvSelected = false;
            }
        }

        partial void OnFormatCsvSelectedChanged(bool value)
        {
            if (value)
            {
                FormatCapyCardSelected = false;
                FormatAnkiSelected = false;
            }
        }

        partial void OnScopeFullDeckSelectedChanged(bool value)
        {
            if (value)
            {
                ScopeSelectedSubDecksSelected = false;
                ScopeSelectedCardsSelected = false;
            }
        }

        partial void OnScopeSelectedSubDecksSelectedChanged(bool value)
        {
            if (value)
            {
                ScopeFullDeckSelected = false;
                ScopeSelectedCardsSelected = false;
            }
        }

        partial void OnScopeSelectedCardsSelectedChanged(bool value)
        {
            if (value)
            {
                ScopeFullDeckSelected = false;
                ScopeSelectedSubDecksSelected = false;
            }
        }
    }
}
