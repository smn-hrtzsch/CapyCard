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
                // Berechne die korrekte nächste Nummer basierend auf der Einrückungsebene
                var nextNumber = CalculateNextNumberForIndentLevel(text, lineStart, indent);
                newPrefix = indent + nextNumber + ". ";
            }
            else
            {
                newPrefix = indent + "- ";
            }
            
            // Füge neue Zeile mit Listenpunkt ein
            var insertText = "\n" + newPrefix;
            var newTextContent = text.Substring(0, cursorPos) + insertText + text.Substring(cursorPos);
            
            // Nummeriere alle Listen im Text neu (korrigiert nachfolgende Einträge)
            newTextContent = RenumberAllLists(newTextContent);
            
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
                string newIndent;
                if (indent.StartsWith("  "))
                {
                    newIndent = indent.Substring(2);
                    cursorDelta = -2;
                }
                else if (indent.StartsWith("\t"))
                {
                    newIndent = indent.Substring(1);
                    cursorDelta = -1;
                }
                else
                {
                    return false; // Kann nicht weiter ausrücken
                }

                // Bei nummerierten Listen: Nummer für höhere Ebene berechnen
                if (isOrdered)
                {
                    var content = currentLine.Substring(prefix.Length);
                    var nextNumber = CalculateNextNumberForIndentLevel(text, lineStart, newIndent);
                    newLine = newIndent + nextNumber + ". " + content;
                }
                else
                {
                    newLine = currentLine.Substring(cursorDelta * -1);
                }
            }
            else
            {
                // Einrücken: Füge 2 Leerzeichen am Anfang hinzu
                var newIndent = indent + "  ";
                cursorDelta = 2;

                // Bei nummerierten Listen: Nummer basierend auf allen Einträgen in dieser Ebene berechnen
                if (isOrdered)
                {
                    var content = currentLine.Substring(prefix.Length);
                    var nextNumber = CountAllEntriesWithIndent(text, newIndent) + 1;
                    newLine = newIndent + nextNumber + ". " + content;
                }
                else
                {
                    // Bei ungeordneten Listen: Nur die Einrückung erhöhen
                    var content = currentLine.Substring(prefix.Length);
                    newLine = newIndent + "- " + content;
                }
            }

            // Ersetze die Zeile
            var newText = text.Substring(0, lineStart) + newLine + text.Substring(lineEnd);
            
            // Nummeriere alle Listen im Text neu
            newText = RenumberAllLists(newText);
            
            EditorTextBox.Text = newText;

            var newCursorPos = Math.Max(lineStart, cursorPos + cursorDelta);
            EditorTextBox.SelectionStart = newCursorPos;
            EditorTextBox.SelectionEnd = newCursorPos;

            return true;
        }

        /// <summary>
        /// Berechnet die nächste Nummer für eine gegebene Einrückungsebene in einer nummerierten Liste.
        /// Zählt alle Einträge inklusive der aktuellen Zeile mit der gleichen Einrückung.
        /// </summary>
        private int CalculateNextNumberForIndentLevel(string text, int currentLineStart, string targetIndent)
        {
            var lines = text.Split('\n');
            var currentLineIndex = -1;
            var charCount = 0;

            // Finde den Index der aktuellen Zeile
            for (int i = 0; i < lines.Length; i++)
            {
                // Prüfe ob der Zeilenanfang an der gesuchten Position ist
                if (charCount == currentLineStart)
                {
                    currentLineIndex = i;
                    break;
                }
                charCount += lines[i].Length + 1; // +1 für '\n'
            }

            // Fallback falls nicht gefunden (sollte nicht passieren)
            if (currentLineIndex < 0)
                currentLineIndex = 0;

            // Zähle alle Einträge bis zur aktuellen Zeile (inklusive) mit gleicher Einrückung
            var count = 0;
            for (int i = 0; i <= currentLineIndex; i++)
            {
                var line = lines[i];
                var match = Regex.Match(line, @"^(\s*)(\d+)\. ");
                if (match.Success)
                {
                    var lineIndent = match.Groups[1].Value;
                    if (lineIndent == targetIndent)
                    {
                        count++;
                    }
                }
            }

            // Die nächste Nummer ist count + 1 (wenn aktuelle Zeile "1." ist, soll nächste "2." sein)
            return count + 1;
        }

        /// <summary>
        /// Zählt ALLE nummerierten Listeneinträge mit einer bestimmten Einrückung im gesamten Text.
        /// Wird verwendet wenn man einrückt, um die korrekte Nummer für die neue Ebene zu bestimmen.
        /// </summary>
        private int CountAllEntriesWithIndent(string text, string targetIndent)
        {
            var lines = text.Split('\n');
            var count = 0;

            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^(\s*)(\d+)\. ");
                if (match.Success)
                {
                    var lineIndent = match.Groups[1].Value;
                    if (lineIndent == targetIndent)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Nummeriert alle nummerierten Listen im Text neu.
        /// Für jede Einrückungsebene werden separate Listen erkannt (getrennt durch Leerzeilen).
        /// Nachfolgende Einträge werden korrigiert wenn neue Zeilen eingefügt werden.
        /// Explizite "1." Eingaben vom User starten immer eine neue Liste.
        /// </summary>
        private string RenumberAllLists(string text)
        {
            var lines = text.Split('\n');
            var result = new System.Collections.Generic.List<string>();
            
            // Counters für jede Einrückungsebene
            var counters = new System.Collections.Generic.Dictionary<string, int>();
            // Speichert ob die vorherige Zeile leer war (für neue Listen-Erkennung)
            var wasPreviousLineEmpty = true; // Am Anfang gilt als "neue Liste"
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var match = Regex.Match(line, @"^(\s*)(\d+)\. ");
                
                if (match.Success)
                {
                    var indent = match.Groups[1].Value;
                    var originalNumber = int.Parse(match.Groups[2].Value);
                    var content = line.Substring(match.Length);
                    
                    // Prüfe ob dies eine neue Liste ist:
                    // 1. Nach Leerzeile oder erste Zeile
                    // 2. ODER: User hat explizit "1." eingegeben (originalNumber == 1) 
                    //    UND es gab schon Einträge in dieser Ebene
                    bool isNewList = wasPreviousLineEmpty || 
                                     (originalNumber == 1 && counters.ContainsKey(indent) && counters[indent] > 0);
                    
                    if (isNewList)
                    {
                        // Neue Liste beginnt - reset counter für diese Ebene
                        counters[indent] = 1;
                    }
                    else if (!counters.ContainsKey(indent))
                    {
                        // Erster Eintrag in dieser Ebene
                        counters[indent] = 1;
                    }
                    else
                    {
                        // Fortlaufende Liste - inkrementiere counter
                        counters[indent]++;
                    }
                    
                    // Ersetze die Zeile mit der korrekten Nummer
                    var newLine = indent + counters[indent] + ". " + content;
                    result.Add(newLine);
                    wasPreviousLineEmpty = false;
                }
                else
                {
                    // Kein Listeneintrag - behalte Zeile bei
                    result.Add(line);
                    // Leerzeilen oder Nicht-List-Zeilen trennen Listen
                    wasPreviousLineEmpty = string.IsNullOrWhiteSpace(line) || !Regex.IsMatch(line, @"^(\s*)- ");
                }
            }
            
            return string.Join('\n', result);
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
            
            // Nummeriere alle Listen neu
            newText = RenumberAllLists(newText);
            
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
