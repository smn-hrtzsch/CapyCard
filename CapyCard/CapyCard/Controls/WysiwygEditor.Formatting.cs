using System;
using Avalonia.Threading;
using CapyCard.Services;

namespace CapyCard.Controls
{
    /// <summary>
    /// WysiwygEditor - Text-Formatierungslogik (Bold, Italic, Underline, Highlight)
    /// </summary>
    public partial class WysiwygEditor
    {
        #region Formatting Logic

        /// <summary>
        /// Wendet Formatierung mit gecachter Selektion an (für Button-Clicks).
        /// </summary>
        private void ApplyFormattingFromCache(string prefix, string suffix)
        {
            ApplyFormattingInternal(prefix, suffix, _cachedSelectionStart, _cachedSelectionEnd);
        }

        /// <summary>
        /// Wendet Formatierung direkt an (für Tastenkombinationen).
        /// </summary>
        private void ApplyFormattingDirect(string prefix, string suffix)
        {
            ApplyFormattingInternal(prefix, suffix, EditorTextBox.SelectionStart, EditorTextBox.SelectionEnd);
        }

        /// <summary>
        /// Interne Methode zum Anwenden von Formatierung.
        /// </summary>
        private void ApplyFormattingInternal(string prefix, string suffix, int selectionStart, int selectionEnd)
        {
            var text = EditorTextBox.Text ?? string.Empty;
            
            // Normalisiere Start und Ende (bei Rückwärts-Selektion ist End < Start)
            var actualStart = Math.Min(selectionStart, selectionEnd);
            var actualEnd = Math.Max(selectionStart, selectionEnd);
            selectionStart = actualStart;
            selectionEnd = actualEnd;
            
            var selectionLength = selectionEnd - selectionStart;

            // Stelle sicher, dass die Werte gültig sind
            selectionStart = Math.Max(0, Math.Min(selectionStart, text.Length));
            selectionLength = Math.Max(0, selectionLength);
            if (selectionStart + selectionLength > text.Length)
            {
                selectionLength = text.Length - selectionStart;
            }

            string newText;
            int newSelStart, newSelEnd;

            if (selectionLength == 0)
            {
                // Kein Text ausgewählt - füge Platzhalter ein
                var placeholder = prefix + "Text" + suffix;
                newText = text.Insert(selectionStart, placeholder);
                newSelStart = selectionStart + prefix.Length;
                newSelEnd = selectionStart + prefix.Length + 4; // "Text" selektieren
            }
            else
            {
                var selectedText = text.Substring(selectionStart, selectionLength);
                
                // Prüfe ob bereits formatiert (im umgebenden Text)
                bool alreadyFormatted = false;
                if (selectionStart >= prefix.Length && 
                    selectionStart + selectionLength + suffix.Length <= text.Length)
                {
                    var beforePrefix = text.Substring(selectionStart - prefix.Length, prefix.Length);
                    var afterSuffix = text.Substring(selectionStart + selectionLength, suffix.Length);
                    alreadyFormatted = beforePrefix == prefix && afterSuffix == suffix;
                }

                if (alreadyFormatted)
                {
                    // Entferne Formatierung
                    newText = text.Remove(selectionStart + selectionLength, suffix.Length);
                    newText = newText.Remove(selectionStart - prefix.Length, prefix.Length);
                    newSelStart = selectionStart - prefix.Length;
                    newSelEnd = newSelStart + selectionLength;
                }
                else
                {
                    // Füge Formatierung hinzu
                    var formatted = prefix + selectedText + suffix;
                    newText = text.Remove(selectionStart, selectionLength).Insert(selectionStart, formatted);
                    newSelStart = selectionStart + prefix.Length;
                    newSelEnd = newSelStart + selectionLength;
                }
            }

            // Aktualisiere Text
            _isUpdating = true;
            EditorTextBox.Text = newText;
            Text = ConvertPlaceholdersToBase64(newText);
            _isUpdating = false;
            
            UpdateFormattedDisplay();
            
            // Dispatch, damit die TextBox den neuen Text verarbeiten kann
            Dispatcher.UIThread.Post(() =>
            {
                EditorTextBox.SelectionStart = newSelStart;
                EditorTextBox.SelectionEnd = newSelEnd;
                EditorTextBox.Focus();
                
                // Cache aktualisieren
                _cachedSelectionStart = newSelStart;
                _cachedSelectionEnd = newSelEnd;
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Fügt Text an der aktuellen Cursor-Position ein.
        /// </summary>
        private void InsertTextAtCursor(string textToInsert)
        {
            var text = EditorTextBox.Text ?? string.Empty;
            var cursorPos = _cachedSelectionStart;
            
            var newText = text.Insert(cursorPos, textToInsert);
            
            _isUpdating = true;
            EditorTextBox.Text = newText;
            Text = ConvertPlaceholdersToBase64(newText);
            _isUpdating = false;
            
            var newCursorPos = cursorPos + textToInsert.Length;
            
            Dispatcher.UIThread.Post(() =>
            {
                EditorTextBox.SelectionStart = newCursorPos;
                EditorTextBox.SelectionEnd = newCursorPos;
                EditorTextBox.Focus();
                _cachedSelectionStart = newCursorPos;
                _cachedSelectionEnd = newCursorPos;
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Fügt Text aus der Zwischenablage ein (wenn kein Bild gefunden wurde).
        /// </summary>
        private async System.Threading.Tasks.Task InsertClipboardTextAsync()
        {
            try
            {
                var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null) return;
                
#pragma warning disable CS0618 // Obsolete API
                var clipboardText = await clipboard.GetTextAsync();
#pragma warning restore CS0618
                
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    var normalizedClipboardText = MarkdownService.NormalizeForPaste(clipboardText);

                    // Ersetze selektierten Text oder füge an Cursor-Position ein
                    var text = EditorTextBox.Text ?? string.Empty;
                    var selStart = Math.Min(_cachedSelectionStart, _cachedSelectionEnd);
                    var selEnd = Math.Max(_cachedSelectionStart, _cachedSelectionEnd);
                    
                    // Grenzen prüfen
                    selStart = Math.Max(0, Math.Min(selStart, text.Length));
                    selEnd = Math.Max(selStart, Math.Min(selEnd, text.Length));
                    
                    var newText = text.Substring(0, selStart) + normalizedClipboardText + text.Substring(selEnd);
                    
                    _isUpdating = true;
                    EditorTextBox.Text = newText;
                    Text = ConvertPlaceholdersToBase64(newText);
                    _isUpdating = false;
                    
                    var newCursorPos = selStart + normalizedClipboardText.Length;
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        EditorTextBox.SelectionStart = newCursorPos;
                        EditorTextBox.SelectionEnd = newCursorPos;
                        EditorTextBox.Focus();
                        _cachedSelectionStart = newCursorPos;
                        _cachedSelectionEnd = newCursorPos;
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InsertClipboardTextAsync] Error: {ex.Message}");
            }
        }

        #endregion
    }
}
