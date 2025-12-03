using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

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

        #endregion

        #region Dependency Properties

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<WysiwygEditor, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<WysiwygEditor, string>(nameof(Watermark), "Text eingeben...");

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

        #endregion

        #region Constructor

        public WysiwygEditor()
        {
            InitializeComponent();
            
            // Initialisiere Inlines für FormattedDisplay
            FormattedDisplay.Inlines = new InlineCollection();
            
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
            
            // TextBox-Text initial unsichtbar (bis fokussiert)
            EditorTextBox.Foreground = Brushes.Transparent;
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
            if (_isUpdating) return;
            
            _isUpdating = true;
            try
            {
                Text = ConvertPlaceholdersToBase64(EditorTextBox.Text ?? string.Empty);
                UpdateFormattedDisplay();
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

        #region Keyboard Handling

        /// <summary>
        /// Tunnel-Handler für KeyDown - wird VOR dem TextBox-internen Handler aufgerufen.
        /// </summary>
        private async void OnEditorKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            bool isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                               e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            
            // Escape: Fokus entfernen
            if (e.Key == Key.Escape)
            {
                // Fokus auf das Parent-Element setzen um aus dem Editor zu gehen
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    // Setze Fokus auf das TopLevel, was effektiv den Fokus vom Editor entfernt
                    topLevel.FocusManager?.ClearFocus();
                }
                e.Handled = true;
                return;
            }
            
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
