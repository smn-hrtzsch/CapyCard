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
using CapyCard.Services.ImportExport.Formats;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IImportExportService _importExportService;
        private Stream? _fileStream;
        private string _fileName = string.Empty;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        // Preview Data
        [ObservableProperty]
        private string _previewFileName = string.Empty;

        [ObservableProperty]
        private string _previewFormatName = string.Empty;

        [ObservableProperty]
        private string _previewCardCount = string.Empty;

        [ObservableProperty]
        private string _previewSubDeckCount = string.Empty;

        [ObservableProperty]
        private bool _previewHasProgress;

        [ObservableProperty]
        private bool _showPreview;

        // Import Target Selection
        [ObservableProperty]
        private bool _isNewDeckSelected = true;

        [ObservableProperty]
        private bool _isExistingDeckSelected;

        [ObservableProperty]
        private string _newDeckName = string.Empty;

        [ObservableProperty]
        private DeckItemViewModel? _selectedExistingDeck;

        public ObservableCollection<DeckItemViewModel> AvailableDecks { get; } = new();

        // Options
        [ObservableProperty]
        private bool _includeProgress = true;

        [ObservableProperty]
        private int _duplicateHandlingIndex; // 0=Skip, 1=Replace, 2=KeepBoth

        public event Action<ImportResult>? OnImportCompleted;
        public event Func<Task<IStorageFile?>>? OnRequestFileOpen;

        public ImportViewModel()
        {
            _importExportService = new ImportExportService();
        }

        public async Task ShowAsync()
        {
            IsVisible = true;
            HasError = false;
            ErrorMessage = string.Empty;
            ShowPreview = false;
            IsNewDeckSelected = true;
            IsExistingDeckSelected = false;
            IncludeProgress = true;
            DuplicateHandlingIndex = 2; // KeepBoth as default

            await LoadAvailableDecksAsync();
        }

        private async Task LoadAvailableDecksAsync()
        {
            AvailableDecks.Clear();
            using var context = new FlashcardDbContext();
            var rootDecks = await context.Decks
                .AsNoTracking()
                .Where(d => d.ParentDeckId == null)
                .ToListAsync();

            foreach (var deck in rootDecks)
            {
                AvailableDecks.Add(new DeckItemViewModel(deck));
            }

            if (AvailableDecks.Any())
            {
                SelectedExistingDeck = AvailableDecks.First();
            }
        }

        [RelayCommand]
        private async Task SelectFile()
        {
            if (OnRequestFileOpen == null) return;

            var file = await OnRequestFileOpen.Invoke();
            if (file == null) return;

            HasError = false;
            ErrorMessage = string.Empty;
            IsAnalyzing = true;
            StatusMessage = "Datei wird analysiert...";

            try
            {
                _fileStream?.Dispose();
                _fileStream = await file.OpenReadAsync();
                _fileName = file.Name;

                PreviewFileName = _fileName;

                var preview = await _importExportService.AnalyzeFileAsync(_fileStream, _fileName);

                if (!preview.Success)
                {
                    HasError = true;
                    ErrorMessage = preview.ErrorMessage ?? "Unbekannter Fehler beim Analysieren.";
                    ShowPreview = false;
                    return;
                }

                PreviewFormatName = preview.FormatName ?? "Unbekannt";
                PreviewCardCount = $"{preview.CardCount} Karten";
                PreviewSubDeckCount = preview.SubDeckCount > 0 ? $"{preview.SubDeckCount} Themen" : "Keine Themen";
                PreviewHasProgress = preview.HasProgress;
                NewDeckName = preview.DeckName ?? Path.GetFileNameWithoutExtension(_fileName);
                ShowPreview = true;

                // Reset stream position for import
                _fileStream.Position = 0;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Fehler: {ex.Message}";
                ShowPreview = false;
            }
            finally
            {
                IsAnalyzing = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private async Task Import()
        {
            if (_fileStream == null || string.IsNullOrEmpty(_fileName))
            {
                HasError = true;
                ErrorMessage = "Bitte wähle zuerst eine Datei aus.";
                return;
            }

            if (IsNewDeckSelected && string.IsNullOrWhiteSpace(NewDeckName))
            {
                HasError = true;
                ErrorMessage = "Bitte gib einen Namen für das neue Fach ein.";
                return;
            }

            if (IsExistingDeckSelected && SelectedExistingDeck == null)
            {
                HasError = true;
                ErrorMessage = "Bitte wähle ein Ziel-Fach aus.";
                return;
            }

            HasError = false;
            IsImporting = true;
            StatusMessage = "Import läuft...";

            try
            {
                _fileStream.Position = 0;

                var options = new ImportOptions
                {
                    Target = IsNewDeckSelected ? ImportTarget.NewDeck : ImportTarget.ExistingDeck,
                    NewDeckName = IsNewDeckSelected ? NewDeckName : null,
                    TargetDeckId = IsExistingDeckSelected ? SelectedExistingDeck?.Deck.Id : null,
                    IncludeProgress = IncludeProgress,
                    OnDuplicate = DuplicateHandlingIndex switch
                    {
                        0 => DuplicateHandling.Skip,
                        1 => DuplicateHandling.Replace,
                        _ => DuplicateHandling.KeepBoth
                    }
                };

                var result = await _importExportService.ImportAsync(_fileStream, _fileName, options);

                if (result.Success)
                {
                    IsVisible = false;
                    OnImportCompleted?.Invoke(result);
                }
                else
                {
                    HasError = true;
                    ErrorMessage = result.ErrorMessage ?? "Import fehlgeschlagen.";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Fehler beim Import: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _fileStream?.Dispose();
            _fileStream = null;
            IsVisible = false;
        }

        partial void OnIsNewDeckSelectedChanged(bool value)
        {
            if (value) IsExistingDeckSelected = false;
        }

        partial void OnIsExistingDeckSelectedChanged(bool value)
        {
            if (value) IsNewDeckSelected = false;
        }
    }
}
