using System;
using System.Text.RegularExpressions;

namespace CapyCard.Services
{
    /// <summary>
    /// Service für Markdown-Parsing und Auto-Formatierung.
    /// Unterstützt: Fett, Kursiv, Unterstrichen, Listen, Bilder.
    /// </summary>
    public static partial class MarkdownService
    {
        // Regex-Patterns für Markdown-Elemente
        private static readonly Regex BoldPattern = GeneratedBoldPattern();
        private static readonly Regex ItalicPattern = GeneratedItalicPattern();
        private static readonly Regex UnderlinePattern = GeneratedUnderlinePattern();
        private static readonly Regex HighlightPattern = GeneratedHighlightPattern();
        private static readonly Regex ImagePattern = GeneratedImagePattern();
        private static readonly Regex UnorderedListPattern = GeneratedUnorderedListPattern();
        private static readonly Regex OrderedListPattern = GeneratedOrderedListPattern();

        [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
        private static partial Regex GeneratedBoldPattern();

        [GeneratedRegex(@"\*(.+?)\*", RegexOptions.Compiled)]
        private static partial Regex GeneratedItalicPattern();

        [GeneratedRegex(@"__(.+?)__", RegexOptions.Compiled)]
        private static partial Regex GeneratedUnderlinePattern();

        [GeneratedRegex(@"==(.+?)==", RegexOptions.Compiled)]
        private static partial Regex GeneratedHighlightPattern();

        [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled)]
        private static partial Regex GeneratedImagePattern();

        [GeneratedRegex(@"^[-*]\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline)]
        private static partial Regex GeneratedUnorderedListPattern();

        [GeneratedRegex(@"^(\d+)\.\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline)]
        private static partial Regex GeneratedOrderedListPattern();

        /// <summary>
        /// Fügt Fett-Formatierung um den ausgewählten Text hinzu.
        /// </summary>
        public static string ApplyBold(string text, int selectionStart, int selectionLength)
        {
            return ApplyFormatting(text, selectionStart, selectionLength, "**", "**");
        }

        /// <summary>
        /// Fügt Kursiv-Formatierung um den ausgewählten Text hinzu.
        /// </summary>
        public static string ApplyItalic(string text, int selectionStart, int selectionLength)
        {
            return ApplyFormatting(text, selectionStart, selectionLength, "*", "*");
        }

        /// <summary>
        /// Fügt Unterstrichen-Formatierung um den ausgewählten Text hinzu.
        /// </summary>
        public static string ApplyUnderline(string text, int selectionStart, int selectionLength)
        {
            return ApplyFormatting(text, selectionStart, selectionLength, "__", "__");
        }

        /// <summary>
        /// Fügt Hervorhebung (Highlight) um den ausgewählten Text hinzu.
        /// </summary>
        public static string ApplyHighlight(string text, int selectionStart, int selectionLength)
        {
            return ApplyFormatting(text, selectionStart, selectionLength, "==", "==");
        }

        /// <summary>
        /// Fügt ein Bild an der Cursor-Position ein.
        /// </summary>
        public static string InsertImage(string text, int position, string imageId, string altText = "Bild")
        {
            var imageMarkdown = $"![{altText}]({imageId})";
            return text.Insert(position, imageMarkdown);
        }

        /// <summary>
        /// Fügt einen Stichpunkt an der aktuellen Zeile hinzu.
        /// </summary>
        public static string ApplyBulletList(string text, int selectionStart)
        {
            return InsertAtLineStart(text, selectionStart, "- ");
        }

        /// <summary>
        /// Fügt eine Nummerierung an der aktuellen Zeile hinzu.
        /// </summary>
        public static string ApplyNumberedList(string text, int selectionStart, int number = 1)
        {
            return InsertAtLineStart(text, selectionStart, $"{number}. ");
        }

        /// <summary>
        /// Prüft ob eine automatische Listenformatierung angewendet werden soll.
        /// Wird aufgerufen wenn der Benutzer ein Leerzeichen eingibt.
        /// </summary>
        /// <returns>True wenn eine Transformation durchgeführt wurde.</returns>
        public static (bool transformed, string newText, int newCaretPos) TryAutoList(string text, int caretPosition)
        {
            // Finde den Zeilenanfang
            int lineStart = text.LastIndexOf('\n', Math.Max(0, caretPosition - 1)) + 1;
            string lineContent = text.Substring(lineStart, caretPosition - lineStart);

            // Prüfe auf "- " (Strichpunkt-Liste)
            if (lineContent == "-")
            {
                // Ersetze "-" durch "• "
                var newText = text.Remove(lineStart, 1).Insert(lineStart, "• ");
                return (true, newText, lineStart + 2);
            }

            // Prüfe auf "* " (Alternative Strichpunkt-Liste)
            if (lineContent == "*")
            {
                var newText = text.Remove(lineStart, 1).Insert(lineStart, "• ");
                return (true, newText, lineStart + 2);
            }

            // Prüfe auf nummerierte Liste "1.", "2.", etc.
            if (lineContent.Length >= 1 && char.IsDigit(lineContent[0]))
            {
                var match = Regex.Match(lineContent, @"^(\d+)\.$");
                if (match.Success)
                {
                    // Behalte die Nummer, füge aber nichts extra hinzu
                    // Die Nummer bleibt, nur das Format wird bestätigt
                    return (false, text, caretPosition);
                }
            }

            return (false, text, caretPosition);
        }

        /// <summary>
        /// Prüft bei Enter ob eine neue Listenzeile eingefügt werden soll.
        /// </summary>
        public static (bool handled, string newText, int newCaretPos) HandleEnterInList(string text, int caretPosition)
        {
            // Finde die aktuelle Zeile
            int lineStart = text.LastIndexOf('\n', Math.Max(0, caretPosition - 1)) + 1;
            int lineEnd = text.IndexOf('\n', caretPosition);
            if (lineEnd == -1) lineEnd = text.Length;
            
            string currentLine = text.Substring(lineStart, lineEnd - lineStart);

            // Prüfe auf Bullet Point
            if (currentLine.TrimStart().StartsWith("• "))
            {
                // Wenn die Zeile nur "• " ist, entferne sie
                if (currentLine.Trim() == "•")
                {
                    var newText = text.Remove(lineStart, currentLine.Length);
                    return (true, newText, lineStart);
                }

                // Füge neue Zeile mit Bullet Point ein
                string newLine = "\n• ";
                var result = text.Insert(caretPosition, newLine);
                return (true, result, caretPosition + newLine.Length);
            }

            // Prüfe auf nummerierte Liste
            var numberMatch = Regex.Match(currentLine.TrimStart(), @"^(\d+)\.\s");
            if (numberMatch.Success)
            {
                int currentNumber = int.Parse(numberMatch.Groups[1].Value);
                
                // Wenn die Zeile nur die Nummer ist, entferne sie
                if (currentLine.Trim() == $"{currentNumber}.")
                {
                    var newText = text.Remove(lineStart, currentLine.Length);
                    return (true, newText, lineStart);
                }

                string newLine = $"\n{currentNumber + 1}. ";
                var result = text.Insert(caretPosition, newLine);
                return (true, result, caretPosition + newLine.Length);
            }

            return (false, text, caretPosition);
        }

        /// <summary>
        /// Extrahiert den Plain-Text aus Markdown (für Vorschau).
        /// </summary>
        public static string StripMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            var result = markdown;

            // Entferne Bilder (ersetze durch [Bild])
            result = ImagePattern.Replace(result, "[Bild]");

            // Entferne Formatierung aber behalte Text
            result = BoldPattern.Replace(result, "$1");
            result = UnderlinePattern.Replace(result, "$1");
            result = HighlightPattern.Replace(result, "$1");
            result = ItalicPattern.Replace(result, "$1");

            // Entferne Bullet-Points
            result = result.Replace("• ", "");

            // Entferne nummerierte Listen-Prefixe
            result = OrderedListPattern.Replace(result, "$2");

            return result.Trim();
        }

        /// <summary>
        /// Prüft ob der Text Markdown-Formatierung enthält.
        /// </summary>
        public static bool ContainsFormatting(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            return BoldPattern.IsMatch(text) ||
                   ItalicPattern.IsMatch(text) ||
                   UnderlinePattern.IsMatch(text) ||
                   HighlightPattern.IsMatch(text) ||
                   ImagePattern.IsMatch(text) ||
                   text.Contains("• ");
        }

        /// <summary>
        /// Extrahiert alle Bild-IDs aus dem Markdown-Text.
        /// </summary>
        public static string[] ExtractImageIds(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return Array.Empty<string>();

            var matches = ImagePattern.Matches(markdown);
            var ids = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                ids[i] = matches[i].Groups[2].Value;
            }
            return ids;
        }

        private static string ApplyFormatting(string text, int selectionStart, int selectionLength, string prefix, string suffix)
        {
            // Sicherheitsprüfung für negative oder ungültige Werte
            selectionStart = Math.Max(0, Math.Min(selectionStart, text.Length));
            selectionLength = Math.Max(0, selectionLength);
            
            // Stelle sicher, dass wir nicht über das Textende hinauslesen
            if (selectionStart + selectionLength > text.Length)
            {
                selectionLength = text.Length - selectionStart;
            }
            
            if (selectionLength == 0)
            {
                // Kein Text ausgewählt - füge Platzhalter ein
                return text.Insert(selectionStart, prefix + "Text" + suffix);
            }

            var selectedText = text.Substring(selectionStart, selectionLength);
            
            // Prüfe ob bereits formatiert
            if (selectedText.StartsWith(prefix) && selectedText.EndsWith(suffix))
            {
                // Entferne Formatierung
                var unformatted = selectedText.Substring(prefix.Length, selectedText.Length - prefix.Length - suffix.Length);
                return text.Remove(selectionStart, selectionLength).Insert(selectionStart, unformatted);
            }

            // Füge Formatierung hinzu
            var formatted = prefix + selectedText + suffix;
            return text.Remove(selectionStart, selectionLength).Insert(selectionStart, formatted);
        }

        private static string InsertAtLineStart(string text, int position, string prefix)
        {
            // Finde den Zeilenanfang
            int lineStart = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
            
            // Prüfe ob bereits ein Prefix vorhanden ist
            if (text.Substring(lineStart).StartsWith(prefix))
            {
                // Entferne Prefix
                return text.Remove(lineStart, prefix.Length);
            }

            return text.Insert(lineStart, prefix);
        }
    }
}
