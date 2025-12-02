using System;
using System.Text.RegularExpressions;
using Avalonia.Input;

namespace CapyCard.Controls
{
    /// <summary>
    /// WysiwygEditor - Listen-Logik (Enter-Taste, Tab-Einrückung)
    /// </summary>
    public partial class WysiwygEditor
    {
        #region List Handling

        /// <summary>
        /// Behandelt Enter-Taste in Listen - fügt automatisch neuen Listenpunkt hinzu.
        /// </summary>
        private bool HandleEnterInList()
        {
            var text = EditorTextBox.Text ?? string.Empty;
            var cursorPos = EditorTextBox.SelectionStart;
            
            // Finde die aktuelle Zeile
            var lineStart = text.LastIndexOf('\n', Math.Max(0, cursorPos - 1)) + 1;
            var lineEnd = text.IndexOf('\n', cursorPos);
            if (lineEnd == -1) lineEnd = text.Length;
            
            var currentLine = text.Substring(lineStart, lineEnd - lineStart);
            
            // Prüfe auf Listenmuster
            var listMatch = GetListPrefix(currentLine);
            if (listMatch == null) return false;
            
            var (prefix, indent, isOrdered, number) = listMatch.Value;
            
            // Wenn die Zeile nur den Listenpunkt enthält (leer), Liste beenden
            var lineContent = currentLine.Substring(prefix.Length).Trim();
            if (string.IsNullOrEmpty(lineContent))
            {
                // Entferne den leeren Listenpunkt
                var newText = text.Substring(0, lineStart) + text.Substring(lineEnd);
                EditorTextBox.Text = newText;
                EditorTextBox.SelectionStart = lineStart;
                EditorTextBox.SelectionEnd = lineStart;
                return true;
            }
            
            // Erstelle neuen Listenpunkt
            string newPrefix;
            if (isOrdered)
            {
                newPrefix = indent + (number + 1) + ". ";
            }
            else
            {
                newPrefix = indent + "- ";
            }
            
            // Füge neue Zeile mit Listenpunkt ein
            var insertText = "\n" + newPrefix;
            var newTextContent = text.Substring(0, cursorPos) + insertText + text.Substring(cursorPos);
            EditorTextBox.Text = newTextContent;
            
            var newCursorPos = cursorPos + insertText.Length;
            EditorTextBox.SelectionStart = newCursorPos;
            EditorTextBox.SelectionEnd = newCursorPos;
            
            return true;
        }

        /// <summary>
        /// Behandelt Tab-Taste in Listen - rückt ein oder aus.
        /// </summary>
        private bool HandleTabInList(bool isShiftPressed)
        {
            var text = EditorTextBox.Text ?? string.Empty;
            var cursorPos = EditorTextBox.SelectionStart;
            
            // Finde die aktuelle Zeile
            var lineStart = text.LastIndexOf('\n', Math.Max(0, cursorPos - 1)) + 1;
            var lineEnd = text.IndexOf('\n', cursorPos);
            if (lineEnd == -1) lineEnd = text.Length;
            
            var currentLine = text.Substring(lineStart, lineEnd - lineStart);
            
            // Prüfe auf Listenmuster
            var listMatch = GetListPrefix(currentLine);
            if (listMatch == null) return false;
            
            var (prefix, indent, isOrdered, number) = listMatch.Value;
            
            string newLine;
            int cursorDelta;
            
            if (isShiftPressed)
            {
                // Ausrücken: Entferne 2 Leerzeichen oder 1 Tab am Anfang
                if (indent.StartsWith("  "))
                {
                    newLine = currentLine.Substring(2);
                    cursorDelta = -2;
                }
                else if (indent.StartsWith("\t"))
                {
                    newLine = currentLine.Substring(1);
                    cursorDelta = -1;
                }
                else
                {
                    return false; // Kann nicht weiter ausrücken
                }
            }
            else
            {
                // Einrücken: Füge 2 Leerzeichen am Anfang hinzu
                newLine = "  " + currentLine;
                cursorDelta = 2;
            }
            
            // Ersetze die Zeile
            var newText = text.Substring(0, lineStart) + newLine + text.Substring(lineEnd);
            EditorTextBox.Text = newText;
            
            var newCursorPos = Math.Max(lineStart, cursorPos + cursorDelta);
            EditorTextBox.SelectionStart = newCursorPos;
            EditorTextBox.SelectionEnd = newCursorPos;
            
            return true;
        }

        /// <summary>
        /// Erkennt Listen-Präfixe in einer Zeile.
        /// Gibt (vollständiges Präfix, Einrückung, istGeordnet, Nummer) zurück.
        /// </summary>
        private (string prefix, string indent, bool isOrdered, int number)? GetListPrefix(string line)
        {
            // Ungeordnete Liste: "  - Text" oder "- Text"
            var unorderedMatch = Regex.Match(line, @"^(\s*)- ");
            if (unorderedMatch.Success)
            {
                var indent = unorderedMatch.Groups[1].Value;
                return (unorderedMatch.Value, indent, false, 0);
            }
            
            // Geordnete Liste: "  1. Text" oder "1. Text"
            var orderedMatch = Regex.Match(line, @"^(\s*)(\d+)\. ");
            if (orderedMatch.Success)
            {
                var indent = orderedMatch.Groups[1].Value;
                var number = int.Parse(orderedMatch.Groups[2].Value);
                return (orderedMatch.Value, indent, true, number);
            }
            
            return null;
        }

        /// <summary>
        /// Fügt einen Listen-Präfix am Zeilenanfang ein.
        /// </summary>
        private void InsertListPrefix(string prefix)
        {
            var text = EditorTextBox.Text ?? string.Empty;
            var cursorPos = _cachedSelectionStart;
            
            // Finde den Zeilenanfang
            var lineStart = text.LastIndexOf('\n', Math.Max(0, cursorPos - 1)) + 1;
            
            // Füge Präfix am Zeilenanfang ein
            var newText = text.Insert(lineStart, prefix);
            
            _isUpdating = true;
            EditorTextBox.Text = newText;
            Text = ConvertPlaceholdersToBase64(newText);
            _isUpdating = false;
            
            // Cursor nach dem Präfix positionieren
            var newCursorPos = cursorPos + prefix.Length;
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EditorTextBox.SelectionStart = newCursorPos;
                EditorTextBox.SelectionEnd = newCursorPos;
                EditorTextBox.Focus();
                _cachedSelectionStart = newCursorPos;
                _cachedSelectionEnd = newCursorPos;
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        #endregion
    }
}
