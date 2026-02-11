using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
        private bool _includeProgress;

        [ObservableProperty]
        private int _duplicateHandlingIndex; // 0=Skip, 1=Replace, 2=KeepBoth

        [ObservableProperty]
        private bool _isLlmImportVisible;

        [ObservableProperty]
        private bool _isCopySuccess;

        [ObservableProperty]
        private string _importJsonText = string.Empty;

        public event Action<ImportResult>? OnImportCompleted;
        public event Func<Task<IStorageFile?>>? OnRequestFileOpen;
        public event Action? RequestShowFormatInfo;
        public ImportViewModel()
        {
            _importExportService = new ImportExportService();
        }
        [RelayCommand]
        private void ShowFormatInfo()
        {
            RequestShowFormatInfo?.Invoke();
        }
        public async Task ShowAsync()
        {
            IsVisible = true;
            IsLlmImportVisible = false;
            ImportJsonText = string.Empty;
            HasError = false;
            ErrorMessage = string.Empty;
            ShowPreview = false;
            IsNewDeckSelected = true;
            IsExistingDeckSelected = false;
            IncludeProgress = false;
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
        private void OpenLlmImport()
        {
            IsLlmImportVisible = true;
            ImportJsonText = string.Empty;
            HasError = false;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void CloseLlmImport()
        {
            IsLlmImportVisible = false;
        }

        [RelayCommand]
        private async Task CopyPrompt()
        {
            if (CapyCard.Services.ClipboardService.Current != null)
            {
                await CapyCard.Services.ClipboardService.Current.SetTextAsync(GenerateSystemPrompt());
                IsCopySuccess = true;
                await Task.Delay(2000);
                IsCopySuccess = false;
            }
        }

        [RelayCommand]
        private async Task AnalyzeText()
        {
            if (string.IsNullOrWhiteSpace(ImportJsonText))
            {
                HasError = true;
                ErrorMessage = "Bitte füge zuerst den JSON-Text ein.";
                return;
            }

            HasError = false;
            ErrorMessage = string.Empty;
            IsAnalyzing = true;
            StatusMessage = "Text wird analysiert...";

            try
            {
                var bytes = Encoding.UTF8.GetBytes(ImportJsonText);
                var stream = new MemoryStream(bytes);
                
                _fileStream?.Dispose();
                _fileStream = stream;
                _fileName = "llm_import.json";

                PreviewFileName = "KI / Text Import";

                var preview = await _importExportService.AnalyzeFileAsync(_fileStream, _fileName);

                if (!preview.Success)
                {
                    HasError = true;
                    ErrorMessage = preview.ErrorMessage ?? "Fehler beim Analysieren des Textes.";
                    ShowPreview = false;
                    return;
                }

                PreviewFormatName = preview.FormatName ?? "JSON";
                PreviewCardCount = $"{preview.CardCount} Karten";
                PreviewSubDeckCount = preview.SubDeckCount > 0 ? $"{preview.SubDeckCount} Themen" : "Keine Themen";
                PreviewHasProgress = preview.HasProgress;
                IncludeProgress = false;
                NewDeckName = preview.DeckName ?? "Mein KI Import";
                
                ShowPreview = true;
                IsLlmImportVisible = false;

                _fileStream.Position = 0;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Fehler bei der Analyse: {ex.Message}";
                ShowPreview = false;
            }
            finally
            {
                IsAnalyzing = false;
                StatusMessage = string.Empty;
            }
        }

        private string GenerateSystemPrompt()
        {
            return @"Du bist ein erfahrener Didaktik-Experte und Prüfungscoach. Deine Aufgabe ist es, aus dem bereitgestellten Lernmaterial hochwertige und umfassende Karteikarten (Flashcards) zu erstellen, die eine optimale Prüfungsvorbereitung ermöglichen.

Ziel: Decke den gesamten Inhalt des Materials so detailliert wie möglich ab. Generiere eine ausführliche Anzahl an Karten, die dem Umfang des Materials gerecht wird.

Regeln für die Erstellung:
1. Umfassende Abdeckung: Extrahiere Fakten, Konzepte, Definitionen und Zusammenhänge systematisch.
2. Qualität: Formuliere klare Fragen (""Warum...?"", ""Wie funktioniert...?"") und präzise Antworten.
   - Vermeide Meta-Kommentare wie ""laut Folie"", ""im Skript"", ""laut Vorlesung"". Stelle Fakten als Fakten dar.
   - Vermeide unnötige Referenzen auf Seitenzahlen oder Foliennummern in der Frage oder Antwort, es sei denn, sie sind Teil des Lerninhalts.

3. Strukturierung: Nutze 'subDecks', um die Karten nach Kapiteln oder Themen zu gliedern.
    - Karten, die das Fach generell betreffen (Allgemeines), kommen direkt in das Haupt-Array ""cards"".
    - WICHTIG: Es gibt nur eine Ebene ""subDecks"" direkt im Root-Objekt.
    - In den Unterthemen selbst KEIN weiteres Feld ""subDecks"" ausgeben (auch nicht als leeres Array).

4. Umfang & Detailtiefe:
   - Ziel ist eine maximale Abdeckung des Materials. Erstelle lieber zu viele als zu wenige Karten.
   - Kombiniere Aufzählungs-Karten (z.B. ""Nenne die 5 Schritte..."") mit Detailkarten, die die einzelnen Schritte erklären.
   - Wichtig: Wenn das Material umfangreich ist, generiere auch entsprechend viele Karten (Deep Dive), anstatt nur Oberflächliches abzufragen.

 Erlaubte Formatierung (WICHTIG - Nutze nur diese):
 - Fett: **Text**
 - Kursiv: *Text*
 - Unterstrichen: __Text__
 - Hervorgehoben (Highlight): ==Text==
 - Listen: Nutzung von ""- "" oder ""1. "" am Zeilenanfang.
 - Tabellen: Pipe-Syntax, z.B. | Begriff | Bedeutung | mit Separator-Zeile | --- | --- |
 - Checklisten: - [ ] Aufgabe und - [x] Erledigt
 - Zitate: > Text
  - Formeln: NUR als gültiges LaTeX. Inline mit $...$ und Block mit $$...$$.
  - Formel-Regel (Strict-LaTeX): Verwende LaTeX-Kommandos (z.B. \Sigma, \Gamma, \exists, \in, \to, \frac{...}{...}).
  - Keine Unicode-/Pseudoformeln wie Σ, Γ, ∃, ∈, → direkt im Formelstring.
  - Bilder: ![Alt](data:image/png;base64,...) (Nur wenn du valide Base64-Daten generieren kannst).
  - NICHT unterstützt: Code-Blöcke (```...```).

JSON-Format (STRENG EINHALTEN):
Antworte AUSSCHLIESSLICH mit einem JSON-Codeblock.
- Beginne mit ```json
- Ende mit ```
- KEIN Text vor oder nach dem Codeblock.
- KEINE erfundenen Metadaten oder Tags außerhalb der Strings.
 - VERBOTEN: Generiere KEINE '[cite_start]', '[cite_end]', '[cite:...]' oder ':contentReference[...]' Tags. Weder innerhalb noch außerhalb der Strings. Dies zerstört das JSON-Format.
 - Alle Zitate oder Quellenangaben müssen TEIL des Strings in ""front"" oder ""back"" sein und als normaler Text formatiert werden (z.B. ""(Quelle: S. 5)"").
  - Für LaTeX in JSON-Strings: Backslashes immer escapen (z.B. ""\\frac{a}{b}"" statt ""\frac{a}{b}"").
  - Zeilenumbrüche innerhalb von JSON-Strings immer als ""\n"" notieren (keine rohen Zeilenumbrüche im String).
  - In jedem Unterthema sind nur ""name"" und ""cards"" erlaubt.

Beispiel für korrektes JSON:
{
  ""name"": ""Thema"",
   ""cards"": [
     {
       ""front"": ""Nenne die Standardform der Geradengleichung."",
       ""back"": ""Die Form ist $y = mx + b$.""
     },
     {
       ""front"": ""Vergleiche Ableitungsregeln."",
       ""back"": ""| Regel | Formel |\n| --- | --- |\n| Produktregel | $f'g + fg'$ |\n| Quotientenregel | $\\frac{f'g - fg'}{g^2}$ |""
     }
   ],
  ""subDecks"": [
    {
      ""name"": ""Unterthema"",
      ""cards"": []
    }
  ]
}";
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
                IncludeProgress = false;
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
            if (_fileStream == null)
            {
                HasError = true;
                ErrorMessage = "Fehler: Datenstrom ist verloren gegangen.";
                return;
            }

            if (!_fileStream.CanRead)
            {
                HasError = true;
                ErrorMessage = "Fehler: Datenstrom wurde bereits geschlossen.";
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
                    },
                    FormatName = PreviewFormatName
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
                ErrorMessage = $"Kritischer Fehler: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private void HandleEscape()
        {
            if (IsLlmImportVisible)
            {
                IsLlmImportVisible = false;
            }
            else if (ShowPreview)
            {
                ShowPreview = false;
                _fileStream?.Dispose();
                _fileStream = null;
            }
            else
            {
                Cancel();
            }
        }

        [RelayCommand]
        private void HandleEnter()
        {
            if (IsLlmImportVisible)
            {
                if (AnalyzeTextCommand.CanExecute(null))
                    AnalyzeTextCommand.Execute(null);
            }
            else if (ShowPreview)
            {
                if (ImportCommand.CanExecute(null))
                    ImportCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _fileStream?.Dispose();
            _fileStream = null;
            IsVisible = false;
            ShowPreview = false;
            IsLlmImportVisible = false;
        }

        partial void OnIsNewDeckSelectedChanged(bool value)
        {
            if (value) 
            {
                _isExistingDeckSelected = false;
                OnPropertyChanged(nameof(IsExistingDeckSelected));
            }
        }

        partial void OnIsExistingDeckSelectedChanged(bool value)
        {
            if (value) 
            {
                _isNewDeckSelected = false;
                OnPropertyChanged(nameof(IsNewDeckSelected));
            }
        }
    }
}
