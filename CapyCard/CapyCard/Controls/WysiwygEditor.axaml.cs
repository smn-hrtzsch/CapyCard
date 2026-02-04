using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using CapyCard.Models;
using CapyCard.Services.TextChecking;
using Material.Icons;
using Material.Icons.Avalonia;

namespace CapyCard.Controls
{
    /// <summary>
    /// Ein WYSIWYG Rich-Text-Editor für Karteikarten.
    /// Speichert intern als Markdown, zeigt aber formatiert an.
    /// Verwendet Overlay-Ansatz: Unsichtbare TextBox über formatiertem TextBlock.
    /// 
    /// Aufgeteilt in mehrere Partial-Class-Dateien:
    /// - WysiwygEditor.axaml.cs (diese Datei): Hauptlogik, Properties, Event-Handler
    /// - WysiwygEditor.Formatting.cs: Text-Formatierung (Bold, Italic, etc.)
    /// - WysiwygEditor.Images.cs: Bildverarbeitung (Insert, Drag&Drop, Clipboard)
    /// - WysiwygEditor.Lists.cs: Listen-Logik (Enter, Tab)
    /// - WysiwygEditor.Parsing.cs: Markdown-Parsing und Anzeige
    /// </summary>
    public partial class WysiwygEditor : UserControl
    {
        #region Static Regex Patterns

        // Regex für Markdown-Parsing
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex UnderlineRegex = new(@"__(.+?)__", RegexOptions.Compiled);
        
        // Regex für Bild-Platzhalter im Editor
        // Format: ![📷Bild 1](placeholder) oder ![img:1](#) (Kompatibilität)
        private static readonly Regex ImagePlaceholderRegex = new(
            @"!\[(?:📷Bild |img:)(\d+)\]\([^)]*\)", 
            RegexOptions.Compiled);

        #endregion

        #region Fields

        // Mapping von Platzhalter-ID zu Base64-Data
        private readonly Dictionary<int, string> _imageDataStore = new();
        private int _nextImageId = 1;

        // Cached selection für Button-Clicks
        private int _cachedSelectionStart;
        private int _cachedSelectionEnd;
        
        // Flag um Update-Loops zu vermeiden
        private bool _isUpdating;
        
        // Speichere die Original-Foreground-Farbe
        private IBrush? _originalForeground;

        private static readonly ITextCheckingService SpellCheckService = new HunspellSpellCheckService();
        private static readonly ConcurrentDictionary<string, byte> SharedIgnoredWords =
            new(StringComparer.OrdinalIgnoreCase);
        private static event Action? SharedIgnoreListChanged;
        private IReadOnlyList<TextIssue> _currentIssues = Array.Empty<TextIssue>();
        private CancellationTokenSource? _spellCheckCts;
        private readonly TimeSpan _spellCheckDelay = TimeSpan.FromMilliseconds(350);
        private readonly ContextMenu _spellcheckMenu = new();
        
        #endregion

        #region Dependency Properties

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<WysiwygEditor, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<WysiwygEditor, string>(nameof(Watermark), "Text eingeben...");
            
        public static readonly StyledProperty<bool> IsToolbarVisibleProperty =
            AvaloniaProperty.Register<WysiwygEditor, bool>(nameof(IsToolbarVisible), true);

        public static readonly StyledProperty<bool> SpellCheckEnabledProperty =
            AvaloniaProperty.Register<WysiwygEditor, bool>(nameof(SpellCheckEnabled), false);

        public static readonly StyledProperty<string> SpellCheckLocaleProperty =
            AvaloniaProperty.Register<WysiwygEditor, string>(nameof(SpellCheckLocale), "de-DE");

        /// <summary>
        /// Der Text im Editor (mit Markdown-Formatierung).
        /// </summary>
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Platzhalter-Text wenn der Editor leer ist.
        /// </summary>
        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }
        
        public bool IsToolbarVisible
        {
            get => GetValue(IsToolbarVisibleProperty);
            set => SetValue(IsToolbarVisibleProperty, value);
        }

        public bool SpellCheckEnabled
        {
            get => GetValue(SpellCheckEnabledProperty);
            set => SetValue(SpellCheckEnabledProperty, value);
        }

        public string SpellCheckLocale
        {
            get => GetValue(SpellCheckLocaleProperty);
            set => SetValue(SpellCheckLocaleProperty, value);
        }

        #endregion

        #region Constructor

        public WysiwygEditor()
        {
            InitializeComponent();
            
            // Initialisiere Inlines für FormattedDisplay
            FormattedDisplay.Inlines = new InlineCollection();
            SpellcheckOverlay.Inlines = new InlineCollection();
            
            // Speichere Selektion bei jeder Änderung
            EditorTextBox.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
            EditorTextBox.AddHandler(KeyUpEvent, OnKeyUpHandler, RoutingStrategies.Tunnel);
            
            // KeyDown im Tunneling-Modus abfangen (vor TextBox-internem Handler)
            EditorTextBox.AddHandler(KeyDownEvent, OnEditorKeyDownTunnel, RoutingStrategies.Tunnel);
            
            // Initiale Sichtbarkeit setzen wenn geladen
            EditorTextBox.Loaded += OnEditorLoaded;
            
            // Drag & Drop aktivieren
            DragDrop.SetAllowDrop(EditorTextBox, true);
            EditorTextBox.AddHandler(DragDrop.DropEvent, OnDrop);
            EditorTextBox.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            
            // Tooltips je nach Betriebssystem anpassen
            UpdateToolbarTooltips();

            EditorTextBox.ContextMenu = _spellcheckMenu;
            _spellcheckMenu.Opened += OnSpellcheckMenuOpened;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SharedIgnoreListChanged += OnSharedIgnoreListChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            SharedIgnoreListChanged -= OnSharedIgnoreListChanged;
            base.OnDetachedFromVisualTree(e);
        }

        #endregion

        #region Lifecycle Events

        /// <summary>
        /// Aktualisiert die Tooltips der Toolbar-Buttons je nach Betriebssystem.
        /// Auf macOS wird "Cmd" angezeigt, auf Windows/Linux "Strg".
        /// </summary>
        private void UpdateToolbarTooltips()
        {
            var modifier = OperatingSystem.IsMacOS() ? "⌘" : "Strg";
            
            ToolTip.SetTip(BoldButton, $"Fett ({modifier}+B)");
            ToolTip.SetTip(ItalicButton, $"Kursiv ({modifier}+I)");
            ToolTip.SetTip(UnderlineButton, $"Unterstrichen ({modifier}+U)");
            ToolTip.SetTip(HighlightButton, $"Hervorheben ({modifier}+H)");
        }

        private void OnEditorLoaded(object? sender, RoutedEventArgs e)
        {
            // Speichere die Original-Foreground-Farbe bevor wir sie ändern
            _originalForeground = EditorTextBox.Foreground;
            
            // Initiale Anzeige: FormattedDisplay zeigen, TextBox-Text unsichtbar
            var isEmpty = string.IsNullOrEmpty(EditorTextBox.Text);
            
            UpdateFormattedDisplay();
            FormattedDisplay.IsVisible = !isEmpty;
            WatermarkDisplay.IsVisible = isEmpty;
            UpdateSpellcheckOverlay();
            UpdateSpellcheckVisibility();
            
            // TextBox-Text initial unsichtbar (bis fokussiert)
            EditorTextBox.Foreground = Brushes.Transparent;
            
            // Initiale Toolbar-Sichtbarkeit basierend auf Property
            UpdateToolbarVisibility(IsToolbarVisible);

            if (SpellCheckEnabled)
            {
                ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty && !_isUpdating)
            {
                _isUpdating = true;
                try
                {
                    // Wenn Text von außen gesetzt wird (z.B. aus DB), 
                    // Base64-Bilder in Platzhalter umwandeln für bessere Editor-Performance
                    var displayText = ConvertBase64ToPlaceholders(Text);
                    
                    if (EditorTextBox.Text != displayText)
                    {
                        EditorTextBox.Text = displayText;
                    }
                    UpdateFormattedDisplay();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
            else if (change.Property == IsToolbarVisibleProperty)
            {
                if (change.NewValue is bool visible)
                {
                    UpdateToolbarVisibility(visible);
                }
            }
            else if (change.Property == SpellCheckEnabledProperty)
            {
                UpdateSpellcheckVisibility();
                if (SpellCheckEnabled)
                {
                    ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
                }
                else
                {
                    ClearSpellcheckIssues();
                }
            }
            else if (change.Property == SpellCheckLocaleProperty)
            {
                if (SpellCheckEnabled)
                {
                    ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
                }
            }
        }

        #endregion

        #region Selection Caching

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            CacheSelection();
        }

        private void OnKeyUpHandler(object? sender, KeyEventArgs e)
        {
            CacheSelection();
        }

        private void CacheSelection()
        {
            _cachedSelectionStart = EditorTextBox.SelectionStart;
            _cachedSelectionEnd = EditorTextBox.SelectionEnd;
        }

        #endregion

        #region TextBox Events

        /// <summary>
        /// Handler für TextBox TextChanged - synchronisiert mit Text Property.
        /// </summary>
        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isUpdating)
            {
                if (SpellCheckEnabled)
                {
                    ClearSpellcheckIssues();
                    ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
                }
                return;
            }
            
            _isUpdating = true;
            try
            {
                Text = ConvertPlaceholdersToBase64(EditorTextBox.Text ?? string.Empty);
                UpdateFormattedDisplay();

                if (SpellCheckEnabled)
                {
                    ClearSpellcheckIssues();
                    ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Handler für Focus gewonnen - zeigt den Rohtext in der TextBox.
        /// </summary>
        private void OnEditorGotFocus(object? sender, GotFocusEventArgs e)
        {
            // TextBox-Text sichtbar machen, formatierte Anzeige ausblenden
            FormattedDisplay.IsVisible = false;
            WatermarkDisplay.IsVisible = false;
            
            // TextBox-Text sichtbar machen (Original-Farbe wiederherstellen)
            if (_originalForeground != null)
            {
                EditorTextBox.Foreground = _originalForeground;
            }

            UpdateSpellcheckOverlay();
            UpdateSpellcheckVisibility();
        }

        /// <summary>
        /// Handler für Focus verloren - zeigt den formatierten Text.
        /// </summary>
        private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
        {
            // Formatierte Anzeige aktualisieren und einblenden
            UpdateFormattedDisplay();
            
            var isEmpty = string.IsNullOrEmpty(EditorTextBox.Text);
            FormattedDisplay.IsVisible = !isEmpty;
            WatermarkDisplay.IsVisible = isEmpty;

            // TextBox-Text unsichtbar machen (FormattedDisplay übernimmt)
            EditorTextBox.Foreground = Brushes.Transparent;

            UpdateSpellcheckVisibility();
        }

        /// <summary>
        /// Aktualisiert die Sichtbarkeit des Watermarks.
        /// </summary>
        private void UpdateWatermarkVisibility()
        {
            var isEmpty = string.IsNullOrEmpty(EditorTextBox.Text);
            var hasFocus = EditorTextBox.IsFocused;
            WatermarkDisplay.IsVisible = isEmpty && !hasFocus;
        }

        #endregion

        #region Spellcheck

        private void ScheduleSpellCheck(string text)
        {
            if (!SpellCheckEnabled)
            {
                return;
            }

            var locale = SpellCheckLocale;
            _spellCheckCts?.Cancel();
            _spellCheckCts?.Dispose();

            var cts = new CancellationTokenSource();
            _spellCheckCts = cts;

            _ = RunSpellCheckAsync(text, locale, cts.Token);
        }

        private async Task RunSpellCheckAsync(string text, string locale, CancellationToken ct)
        {
            try
            {
                await Task.Delay(_spellCheckDelay, ct).ConfigureAwait(false);

                var issues = await Task.Run(
                    () => SpellCheckService.CheckAsync(text, locale, ct).GetAwaiter().GetResult(),
                    ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (!SharedIgnoredWords.IsEmpty)
                {
                    issues = issues.Where(issue => !SharedIgnoredWords.ContainsKey(issue.Word)).ToList();
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!string.Equals(EditorTextBox.Text ?? string.Empty, text, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _currentIssues = issues;
                    UpdateSpellcheckOverlay();
                }, DispatcherPriority.Background);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void ClearSpellcheckIssues()
        {
            _currentIssues = Array.Empty<TextIssue>();
            UpdateSpellcheckOverlay();
        }

        private void UpdateSpellcheckOverlay()
        {
            if (SpellcheckOverlay.Inlines == null)
            {
                SpellcheckOverlay.Inlines = new InlineCollection();
            }

            SpellcheckOverlay.Inlines.Clear();

            if (!SpellCheckEnabled)
            {
                return;
            }

            var text = EditorTextBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var inlines = BuildSpellcheckInlines(text, _currentIssues);
            foreach (var inline in inlines)
            {
                SpellcheckOverlay.Inlines.Add(inline);
            }
        }


        private List<Inline> BuildSpellcheckInlines(string text, IReadOnlyList<TextIssue> issues)
        {
            var inlines = new List<Inline>();
            if (string.IsNullOrEmpty(text))
            {
                return inlines;
            }

            if (issues.Count == 0)
            {
                inlines.Add(new Run(text));
                return inlines;
            }

            var orderedIssues = issues.OrderBy(issue => issue.Start).ToList();
            var decorations = CreateSpellingDecorations();
            var cursor = 0;

            foreach (var issue in orderedIssues)
            {
                if (issue.Start < cursor)
                {
                    continue;
                }

                if (issue.Start > text.Length)
                {
                    break;
                }

                if (issue.Start > cursor)
                {
                    inlines.Add(new Run(text.Substring(cursor, issue.Start - cursor)));
                }

                var length = Math.Min(issue.Length, text.Length - issue.Start);
                if (length > 0)
                {
                    var run = new Run(text.Substring(issue.Start, length))
                    {
                        TextDecorations = decorations
                    };
                    inlines.Add(run);
                }

                cursor = issue.Start + length;
            }

            if (cursor < text.Length)
            {
                inlines.Add(new Run(text.Substring(cursor)));
            }

            return inlines;
        }

        private TextDecorationCollection CreateSpellingDecorations()
        {
            var brush = ResolveUnderlineBrush();

            var decoration = new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Stroke = brush,
                StrokeThickness = 1
            };

            return new TextDecorationCollection { decoration };
        }

        private static IImmutableSolidColorBrush ResolveUnderlineBrush()
        {
            var theme = Application.Current?.ActualThemeVariant;
            if (Application.Current?.Resources.TryGetResource("SpellingUnderlineBrush", theme, out var resource) == true)
            {
                if (resource is IImmutableSolidColorBrush immutableBrush)
                {
                    return immutableBrush;
                }

                if (resource is ISolidColorBrush solidBrush)
                {
                    return new ImmutableSolidColorBrush(solidBrush.Color);
                }
            }

            return Brushes.Red;
        }

        private void UpdateSpellcheckVisibility()
        {
            SpellcheckOverlay.IsVisible = SpellCheckEnabled && EditorTextBox.IsFocused;
        }

        private void OnSpellcheckMenuOpened(object? sender, EventArgs e)
        {
            var items = new List<object>();

            if (!SpellCheckEnabled)
            {
                items.Add(new MenuItem { Header = "Rechtschreibpruefung deaktiviert", IsEnabled = false });
                ReplaceSpellcheckMenuItems(items);
                return;
            }

            var issue = GetIssueAtCaret();
            if (issue == null)
            {
                items.Add(new MenuItem { Header = "Keine Vorschlaege", IsEnabled = false });
                ReplaceSpellcheckMenuItems(items);
                return;
            }

            if (issue.Suggestions.Count > 0)
            {
                foreach (var suggestion in issue.Suggestions)
                {
                    var suggestionText = suggestion;
                    var suggestionItem = new MenuItem { Header = suggestionText };
                    suggestionItem.Click += (_, __) => ApplySuggestion(issue, suggestionText);
                    items.Add(suggestionItem);
                }
            }
            else
            {
                items.Add(new MenuItem { Header = "Keine Vorschlaege", IsEnabled = false });
            }

            items.Add(new Separator());

            var addItem = new MenuItem { Header = "Zum Wörterbuch hinzufügen" };
            addItem.Click += async (_, __) => await AddWordToDictionaryAsync(issue);
            items.Add(addItem);

            items.Add(new Separator());

            var ignoreItem = new MenuItem { Header = "Ignorieren" };
            ignoreItem.Click += (_, __) => IgnoreWord(issue.Word);
            items.Add(ignoreItem);

            ReplaceSpellcheckMenuItems(items);
        }

        private void ReplaceSpellcheckMenuItems(IEnumerable<object> items)
        {
            _spellcheckMenu.Items.Clear();
            foreach (var item in items)
            {
                _spellcheckMenu.Items.Add(item);
            }
        }

        private TextIssue? GetIssueAtCaret()
        {
            var text = EditorTextBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var caretIndex = Math.Clamp(EditorTextBox.SelectionStart, 0, text.Length);
            if (caretIndex == text.Length && caretIndex > 0)
            {
                caretIndex--;
            }

            var wordRange = GetWordRangeAtIndex(text, caretIndex);
            if (wordRange == null)
            {
                return null;
            }

            var (start, length) = wordRange.Value;
            return _currentIssues.FirstOrDefault(issue => issue.Start == start && issue.Length == length);
        }

        private static (int Start, int Length)? GetWordRangeAtIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (index < 0 || index >= text.Length)
            {
                return null;
            }

            if (!char.IsLetter(text[index]))
            {
                if (index > 0 && char.IsLetter(text[index - 1]))
                {
                    index--;
                }
                else
                {
                    return null;
                }
            }

            var start = index;
            while (start > 0 && char.IsLetter(text[start - 1]))
            {
                start--;
            }

            var end = index;
            while (end < text.Length && char.IsLetter(text[end]))
            {
                end++;
            }

            return (start, end - start);
        }

        private void ApplySuggestion(TextIssue issue, string suggestion)
        {
            var text = EditorTextBox.Text ?? string.Empty;
            if (issue.Start < 0 || issue.Start + issue.Length > text.Length)
            {
                return;
            }

            var updatedText = text.Remove(issue.Start, issue.Length)
                .Insert(issue.Start, suggestion);

            _isUpdating = true;
            EditorTextBox.Text = updatedText;
            Text = ConvertPlaceholdersToBase64(updatedText);
            _isUpdating = false;

            UpdateFormattedDisplay();
            ClearSpellcheckIssues();

            var newCaret = issue.Start + suggestion.Length;
            Dispatcher.UIThread.Post(() =>
            {
                EditorTextBox.SelectionStart = newCaret;
                EditorTextBox.SelectionEnd = newCaret;
                CacheSelection();
                EditorTextBox.Focus();
            }, DispatcherPriority.Background);

            ScheduleSpellCheck(updatedText);
        }

        private void IgnoreWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return;
            }

            if (SharedIgnoredWords.TryAdd(word, 0))
            {
                SharedIgnoreListChanged?.Invoke();
            }
            ScheduleSpellCheck(EditorTextBox.Text ?? string.Empty);
        }

        private async Task AddWordToDictionaryAsync(TextIssue issue)
        {
            if (issue == null)
            {
                return;
            }

            var text = EditorTextBox.Text ?? string.Empty;
            await SpellCheckService.AddWordAsync(issue.Word, SpellCheckLocale, CancellationToken.None);
            if (SharedIgnoredWords.TryRemove(issue.Word, out _))
            {
                SharedIgnoreListChanged?.Invoke();
            }
            ClearSpellcheckIssues();
            ScheduleSpellCheck(text);
        }

        private void OnSharedIgnoreListChanged()
        {
            if (!SpellCheckEnabled)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!SpellCheckEnabled)
                {
                    return;
                }

                var text = EditorTextBox.Text ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    ClearSpellcheckIssues();
                    return;
                }

                ScheduleSpellCheck(text);
            }, DispatcherPriority.Background);
        }

        #endregion

        #region Keyboard Handling

        /// <summary>
        /// Tunnel-Handler für KeyDown - wird VOR dem TextBox-internen Handler aufgerufen.
        /// </summary>
        private async void OnEditorKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            bool isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                               e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            
            // Ctrl/Cmd+V: Clipboard-Bild einfügen (muss im Tunnel sein, um vor TextBox zu kommen)
            if (isCtrlOrCmd && e.Key == Key.V)
            {
                // WICHTIG: Event SOFORT als handled markieren, bevor async
                // Sonst führt die TextBox parallel ihr eigenes Paste aus
                e.Handled = true;
                
                var imageInserted = await HandleClipboardPasteAsync();
                if (!imageInserted)
                {
                    // Kein Bild gefunden - normalen Text manuell einfügen
                    await InsertClipboardTextAsync();
                }
                return;
            }
            
            // Escape: Fokus entfernen - muss im Tunneling-Handler sein, damit es vor der TextBox greift
            // Die TextBox konsumiert Escape standardmäßig nicht, aber sicher ist sicher.
            // WICHTIG: Das eigentliche Fokus-Entfernen muss die View machen, hier signalisieren wir nur "nicht handled"
            // oder entfernen den Fokus explizit.
            if (e.Key == Key.Escape)
            {
                // Fokus auf das Parent-Element setzen um aus dem Editor zu gehen
                // Wir suchen das TopLevel (Window) und löschen den Fokus
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    topLevel.FocusManager?.ClearFocus();
                    e.Handled = true;
                    return;
                }
            }

            // Enter-Taste: Listen automatisch fortsetzen
            if (e.Key == Key.Enter && !isCtrlOrCmd)
            {
                if (HandleEnterInList())
                {
                    e.Handled = true;
                    return;
                }
            }

            // Tab-Taste: Listen einrücken
            if (e.Key == Key.Tab)
            {
                bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (HandleTabInList(isShift))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnEditorKeyDown(object? sender, KeyEventArgs e)
        {
            bool isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                               e.KeyModifiers.HasFlag(KeyModifiers.Meta);

            if (isCtrlOrCmd)
            {
                switch (e.Key)
                {
                    case Key.B:
                        ApplyFormattingDirect("**", "**");
                        e.Handled = true;
                        return;
                    case Key.I:
                        ApplyFormattingDirect("*", "*");
                        e.Handled = true;
                        return;
                    case Key.U:
                        ApplyFormattingDirect("__", "__");
                        e.Handled = true;
                        return;
                    case Key.H:
                        ApplyFormattingDirect("==", "==");
                        e.Handled = true;
                        return;
                    // Ctrl+V wird jetzt im Tunnel-Handler behandelt
                }
            }
        }

        #endregion

        #region Toolbar Click Handlers

        private void OnBoldClick(object? sender, RoutedEventArgs e)
        {
            ApplyFormattingFromCache("**", "**");
        }

        private void OnItalicClick(object? sender, RoutedEventArgs e)
        {
            ApplyFormattingFromCache("*", "*");
        }

        private void OnUnderlineClick(object? sender, RoutedEventArgs e)
        {
            ApplyFormattingFromCache("__", "__");
        }

        private void OnBulletListClick(object? sender, RoutedEventArgs e)
        {
            InsertListPrefix("- ");
        }

        private void OnNumberListClick(object? sender, RoutedEventArgs e)
        {
            InsertListPrefix("1. ");
        }

        private void OnHighlightClick(object? sender, RoutedEventArgs e)
        {
            ApplyFormattingFromCache("==", "==");
        }

        private async void OnPasteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!await HandleClipboardPasteAsync())
            {
                await InsertClipboardTextAsync();
            }
        }

        private async void OnImageClick(object? sender, RoutedEventArgs e)
        {
            await InsertImageAsync();
        }

        private void OnToggleToolbarClick(object? sender, RoutedEventArgs e)
        {
            // Toggle lokalen Zustand (via Property)
            IsToolbarVisible = !IsToolbarVisible;
        }
        
        private void UpdateToolbarVisibility(bool visible)
        {
            // Toolbar ein- oder ausblenden
            ToolbarBorder.IsVisible = visible;
            
            // Icon wechseln je nach Zustand (EyeOff = Toolbar ist sichtbar, Eye = Toolbar ist ausgeblendet)
            if (visible)
            {
                ToggleToolbarIcon.Kind = Material.Icons.MaterialIconKind.EyeOff;
                ToolTip.SetTip(ToggleToolbarButton, "Toolbar ausblenden");
            }
            else
            {
                ToggleToolbarIcon.Kind = Material.Icons.MaterialIconKind.Eye;
                ToolTip.SetTip(ToggleToolbarButton, "Toolbar einblenden");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Setzt den Fokus auf den Editor.
        /// </summary>
        public void FocusEditor()
        {
            EditorTextBox.Focus();
        }

        #endregion
    }
}
