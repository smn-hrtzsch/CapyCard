using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CapyCard.Models;
using CapyCard.Services;

namespace CapyCard.Controls
{
    /// <summary>
    /// Ein einfacher Rich-Text-Editor für Karteikarten.
    /// Unterstützt: Fett, Kursiv, Unterstrichen, Listen (automatisch), Bilder.
    /// </summary>
    public partial class RichTextEditor : UserControl
    {
        // Dependency Properties
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<RichTextEditor, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<RichTextEditor, string>(nameof(Watermark), "Text eingeben...");

        /// <summary>
        /// Der Markdown-Text im Editor.
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

        /// <summary>
        /// Temporäre Bilder die noch nicht gespeichert wurden.
        /// Key = ImageId, Value = CardImage
        /// </summary>
        public Dictionary<string, CardImage> PendingImages { get; } = new();

        /// <summary>
        /// Event das ausgelöst wird, wenn ein neues Bild hinzugefügt wurde.
        /// </summary>
        public event EventHandler<CardImage>? ImageAdded;

        private bool _suppressTextChangedHandling = false;

        public RichTextEditor()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Setzt den Fokus auf den Editor.
        /// </summary>
        public void FocusEditor()
        {
            Editor.Focus();
        }

        /// <summary>
        /// Gibt alle pending Images zurück und leert die Liste.
        /// Wird aufgerufen wenn die Karte gespeichert wird.
        /// </summary>
        public List<CardImage> GetAndClearPendingImages()
        {
            var images = new List<CardImage>(PendingImages.Values);
            PendingImages.Clear();
            return images;
        }

        #region Toolbar Click Handlers

        private void OnBoldClick(object? sender, RoutedEventArgs e)
        {
            ApplyBold();
        }

        private void OnItalicClick(object? sender, RoutedEventArgs e)
        {
            ApplyItalic();
        }

        private void OnUnderlineClick(object? sender, RoutedEventArgs e)
        {
            ApplyUnderline();
        }

        private void OnBulletListClick(object? sender, RoutedEventArgs e)
        {
            ApplyBulletList();
        }

        private void OnNumberListClick(object? sender, RoutedEventArgs e)
        {
            ApplyNumberedList();
        }

        private async void OnImageClick(object? sender, RoutedEventArgs e)
        {
            await InsertImage();
        }

        private void OnHighlightClick(object? sender, RoutedEventArgs e)
        {
            ApplyHighlight();
        }

        #endregion

        #region Formatting Methods

        private void ApplyBold()
        {
            var text = Text ?? string.Empty;
            var selectionLength = Math.Max(0, Editor.SelectionEnd - Editor.SelectionStart);
            var newText = MarkdownService.ApplyBold(text, Editor.SelectionStart, selectionLength);
            UpdateTextAndCaret(newText, Editor.SelectionStart + 2 + (selectionLength > 0 ? selectionLength : 4));
        }

        private void ApplyItalic()
        {
            var text = Text ?? string.Empty;
            var selectionLength = Math.Max(0, Editor.SelectionEnd - Editor.SelectionStart);
            var newText = MarkdownService.ApplyItalic(text, Editor.SelectionStart, selectionLength);
            UpdateTextAndCaret(newText, Editor.SelectionStart + 1 + (selectionLength > 0 ? selectionLength : 4));
        }

        private void ApplyUnderline()
        {
            var text = Text ?? string.Empty;
            var selectionLength = Math.Max(0, Editor.SelectionEnd - Editor.SelectionStart);
            var newText = MarkdownService.ApplyUnderline(text, Editor.SelectionStart, selectionLength);
            UpdateTextAndCaret(newText, Editor.SelectionStart + 2 + (selectionLength > 0 ? selectionLength : 4));
        }

        private void ApplyBulletList()
        {
            var text = Text ?? string.Empty;
            var newText = MarkdownService.ApplyBulletList(text, Editor.SelectionStart);
            Text = newText;
        }

        private void ApplyNumberedList()
        {
            var text = Text ?? string.Empty;
            var newText = MarkdownService.ApplyNumberedList(text, Editor.SelectionStart);
            Text = newText;
        }

        private void ApplyHighlight()
        {
            var text = Text ?? string.Empty;
            var selectionLength = Math.Max(0, Editor.SelectionEnd - Editor.SelectionStart);
            var newText = MarkdownService.ApplyHighlight(text, Editor.SelectionStart, selectionLength);
            UpdateTextAndCaret(newText, Editor.SelectionStart + 2 + (selectionLength > 0 ? selectionLength : 4));
        }

        private async Task InsertImage()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Bild auswählen",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Bilder")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp" },
                        MimeTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/webp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                try
                {
                    await using var stream = await file.OpenReadAsync();
                    var mimeType = GetMimeTypeFromFileName(file.Name);
                    var cardImage = await ImageService.CreateCardImageAsync(stream, mimeType, file.Name);

                    // Bild zur pending Liste hinzufügen
                    PendingImages[cardImage.ImageId] = cardImage;

                    // Markdown einfügen
                    var text = Text ?? string.Empty;
                    var newText = MarkdownService.InsertImage(text, Editor.SelectionStart, cardImage.ImageId, file.Name);
                    Text = newText;

                    ImageAdded?.Invoke(this, cardImage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Laden des Bildes: {ex.Message}");
                }
            }
        }

        private static string GetMimeTypeFromFileName(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
        }

        private void UpdateTextAndCaret(string newText, int caretPosition)
        {
            _suppressTextChangedHandling = true;
            Text = newText;
            _suppressTextChangedHandling = false;
            
            // Setze Caret-Position nach dem Update
            Editor.CaretIndex = Math.Min(caretPosition, newText.Length);
        }

        #endregion

        #region Keyboard & Text Handling

        private void Editor_OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Keyboard shortcuts
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                switch (e.Key)
                {
                    case Key.B:
                        ApplyBold();
                        e.Handled = true;
                        break;
                    case Key.I:
                        ApplyItalic();
                        e.Handled = true;
                        break;
                    case Key.U:
                        ApplyUnderline();
                        e.Handled = true;
                        break;
                }
            }

            // Enter-Handling für Listen
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                var text = Text ?? string.Empty;
                var (handled, newText, newCaretPos) = MarkdownService.HandleEnterInList(text, Editor.CaretIndex);
                
                if (handled)
                {
                    e.Handled = true;
                    UpdateTextAndCaret(newText, newCaretPos);
                }
            }
            
            // Tab-Handling für Listen-Einrückung
            if (e.Key == Key.Tab)
            {
                var text = Text ?? string.Empty;
                bool shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                var (handled, newText, newCaretPos) = MarkdownService.HandleTabInList(text, Editor.CaretIndex, shiftPressed);
                
                if (handled)
                {
                    e.Handled = true;
                    UpdateTextAndCaret(newText, newCaretPos);
                }
            }
        }

        private void Editor_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressTextChangedHandling) return;

            var text = Text ?? string.Empty;
            var caretPos = Editor.CaretIndex;

            // Prüfe auf Auto-Listen nur wenn ein Leerzeichen eingegeben wurde
            if (caretPos > 0 && caretPos <= text.Length && text[caretPos - 1] == ' ')
            {
                var (transformed, newText, newCaretPos) = MarkdownService.TryAutoList(text, caretPos);
                
                if (transformed)
                {
                    UpdateTextAndCaret(newText, newCaretPos);
                }
            }
        }

        #endregion
    }
}
