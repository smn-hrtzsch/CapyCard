using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CapyCard.Services
{
    /// <summary>
    /// Central markdown parser and normalizer used by editor, preview and exports.
    /// </summary>
    public static class MarkdownService
    {
        private static readonly Regex CollapseBlankLinesRegex = new(@"\n{3,}", RegexOptions.Compiled);
        private static readonly Regex FenceStartRegex = new(@"^\s*```[a-zA-Z0-9_-]*\s*$", RegexOptions.Compiled);
        private static readonly Regex FenceEndRegex = new(@"^\s*```\s*$", RegexOptions.Compiled);

        private static readonly Regex ChecklistRegex = new(@"^(\s*)-\s\[( |x|X)\]\s?(.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new(@"^(\s*)(\d+)\.\s(.*)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new(@"^(\s*)-\s(.*)$", RegexOptions.Compiled);
        private static readonly Regex QuoteRegex = new(@"^\s*(>+)\s?(.*)$", RegexOptions.Compiled);

        private static readonly Regex TableSeparatorRegex = new(
            @"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex InlineImageRegex = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex InlineFormulaRegex = new(@"(?<!\\)\$(?!\$)(.+?)(?<!\\)\$", RegexOptions.Compiled);
        private static readonly Regex InlineFormattingRegex = new(
            @"(\*\*(.+?)\*\*)|(__(.+?)__)|(==(.+?)==)|(\*(.+?)\*)",
            RegexOptions.Compiled);

        /// <summary>
        /// Officially supported markdown MVP features.
        /// </summary>
        public static IReadOnlyList<string> SupportedMvpFeatures { get; } = new[]
        {
            "Bold (**text**)",
            "Italic (*text*)",
            "Underline (__text__)",
            "Highlight (==text==)",
            "Lists (- / 1.)",
            "Images ![alt](url)",
            "Tables (pipe syntax)",
            "Checklists (- [ ] / - [x])",
            "Quotes (>)",
            "Formulas (strict LaTeX in $...$ / $$...$$)"
        };

        /// <summary>
        /// Legacy markdown formats that stay unchanged for backward compatibility.
        /// </summary>
        public static IReadOnlyList<string> BackwardCompatibleFeatures { get; } = new[]
        {
            "Bold (**text**)",
            "Italic (*text*)",
            "Underline (__text__)",
            "Highlight (==text==)",
            "Lists (- / 1.)",
            "Images ![alt](url)"
        };

        public static string NormalizeInput(string? markdown, bool stripOuterCodeFences = false)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            var normalized = markdown
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            if (stripOuterCodeFences)
            {
                normalized = StripOuterCodeFence(normalized);
            }

            normalized = CollapseBlankLinesRegex.Replace(normalized, "\n\n");
            return normalized;
        }

        public static string NormalizeForPaste(string? markdown)
        {
            return NormalizeInput(markdown, stripOuterCodeFences: true);
        }

        public static MarkdownDocument Parse(string? markdown)
        {
            var normalized = NormalizeInput(markdown);
            if (string.IsNullOrEmpty(normalized))
            {
                return new MarkdownDocument(Array.Empty<MarkdownBlock>());
            }

            var lines = normalized.Split('\n');
            var blocks = new List<MarkdownBlock>(lines.Length);

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];

                if (string.IsNullOrWhiteSpace(line))
                {
                    blocks.Add(new MarkdownBlankLineBlock());
                    continue;
                }

                if (TryParseFormulaBlock(lines, ref index, out var formulaBlock))
                {
                    blocks.Add(formulaBlock);
                    continue;
                }

                if (TryParseTableBlock(lines, ref index, out var tableBlock))
                {
                    blocks.Add(tableBlock);
                    continue;
                }

                if (TryParseChecklistBlock(lines, ref index, out var checklistBlock))
                {
                    blocks.Add(checklistBlock);
                    continue;
                }

                if (TryParseQuoteBlock(lines, ref index, out var quoteBlock))
                {
                    blocks.Add(quoteBlock);
                    continue;
                }

                if (TryParseListBlock(lines, ref index, ordered: true, out var orderedListBlock))
                {
                    blocks.Add(orderedListBlock);
                    continue;
                }

                if (TryParseListBlock(lines, ref index, ordered: false, out var unorderedListBlock))
                {
                    blocks.Add(unorderedListBlock);
                    continue;
                }

                blocks.Add(new MarkdownParagraphBlock(ParseInline(line)));
            }

            return new MarkdownDocument(blocks);
        }

        public static IReadOnlyList<MarkdownInline> ParseInline(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<MarkdownInline>();
            }

            var inlines = new List<MarkdownInline>();
            var cursor = 0;

            while (cursor < text.Length)
            {
                var protectedMatch = GetNextProtectedInlineMatch(text, cursor);
                if (protectedMatch == null)
                {
                    AddFormattedText(inlines, text.Substring(cursor));
                    break;
                }

                if (protectedMatch.Value.Match.Index > cursor)
                {
                    AddFormattedText(inlines, text.Substring(cursor, protectedMatch.Value.Match.Index - cursor));
                }

                var match = protectedMatch.Value.Match;
                switch (protectedMatch.Value.Kind)
                {
                    case ProtectedInlineKind.Image:
                        inlines.Add(new MarkdownImageInline(match.Groups[1].Value, match.Groups[2].Value));
                        break;
                    case ProtectedInlineKind.Formula:
                        inlines.Add(new MarkdownFormulaInline(match.Groups[1].Value));
                        break;
                }

                cursor = match.Index + match.Length;
            }

            return inlines;
        }

        public static bool ContainsExtendedSyntax(string? markdown)
        {
            var document = Parse(markdown);

            foreach (var block in document.Blocks)
            {
                switch (block)
                {
                    case MarkdownChecklistBlock:
                    case MarkdownQuoteBlock:
                    case MarkdownTableBlock:
                    case MarkdownFormulaBlock:
                        return true;
                    case MarkdownParagraphBlock paragraphBlock when ContainsInlineFormula(paragraphBlock.Inlines):
                        return true;
                    case MarkdownListBlock listBlock when listBlock.Items.Any(static item => ContainsInlineFormula(item.Inlines)):
                        return true;
                }
            }

            return false;
        }

        public static string ToPlainText(string? markdown)
        {
            var document = Parse(markdown);
            return ToPlainTextInternal(document, includeFormulaDelimiters: true, includeImages: true);
        }

        public static string StripMarkdown(string? markdown)
        {
            var document = Parse(markdown);
            return ToPlainTextInternal(document, includeFormulaDelimiters: false, includeImages: false);
        }

        private static string ToPlainTextInternal(
            MarkdownDocument document,
            bool includeFormulaDelimiters,
            bool includeImages)
        {
            var lines = new List<string>();

            foreach (var block in document.Blocks)
            {
                switch (block)
                {
                    case MarkdownBlankLineBlock:
                        lines.Add(string.Empty);
                        break;

                    case MarkdownParagraphBlock paragraphBlock:
                        lines.Add(FlattenInlines(paragraphBlock.Inlines, includeFormulaDelimiters, includeImages));
                        break;

                    case MarkdownListBlock listBlock:
                        for (var index = 0; index < listBlock.Items.Count; index++)
                        {
                            var item = listBlock.Items[index];
                            var indent = new string(' ', item.IndentLevel * 2);
                            var prefix = listBlock.IsOrdered
                                ? $"{item.Number ?? index + 1}. "
                                : "- ";
                            var content = FlattenInlines(item.Inlines, includeFormulaDelimiters, includeImages);
                            lines.Add(indent + prefix + content);
                        }
                        break;

                    case MarkdownChecklistBlock checklistBlock:
                        foreach (var item in checklistBlock.Items)
                        {
                            var indent = new string(' ', item.IndentLevel * 2);
                            var prefix = item.IsChecked ? "- [x] " : "- [ ] ";
                            var content = FlattenInlines(item.Inlines, includeFormulaDelimiters, includeImages);
                            lines.Add(indent + prefix + content);
                        }
                        break;

                    case MarkdownQuoteBlock quoteBlock:
                        foreach (var line in quoteBlock.Lines)
                        {
                            var prefix = new string('>', Math.Max(1, line.Level));
                            var content = FlattenInlines(line.Inlines, includeFormulaDelimiters, includeImages);
                            lines.Add($"{prefix} {content}".TrimEnd());
                        }
                        break;

                    case MarkdownTableBlock tableBlock:
                        lines.Add(ToPlainTableRow(tableBlock.Header, includeFormulaDelimiters, includeImages));
                        lines.Add(ToPlainTableSeparator(Math.Max(1, tableBlock.Header.Count)));
                        foreach (var row in tableBlock.Rows)
                        {
                            lines.Add(ToPlainTableRow(row, includeFormulaDelimiters, includeImages));
                        }
                        break;

                    case MarkdownFormulaBlock formulaBlock:
                        if (includeFormulaDelimiters)
                        {
                            lines.Add("$$");
                        }

                        if (!string.IsNullOrEmpty(formulaBlock.Content))
                        {
                            lines.AddRange(formulaBlock.Content.Split('\n'));
                        }

                        if (includeFormulaDelimiters)
                        {
                            lines.Add("$$");
                        }
                        break;
                }
            }

            return string.Join("\n", lines).Trim();
        }

        private static string ToPlainTableRow(
            IReadOnlyList<MarkdownTableCell> cells,
            bool includeFormulaDelimiters,
            bool includeImages)
        {
            var content = cells
                .Select(cell => FlattenInlines(cell.Inlines, includeFormulaDelimiters, includeImages))
                .ToArray();

            return "| " + string.Join(" | ", content) + " |";
        }

        private static string ToPlainTableSeparator(int columnCount)
        {
            var safeColumnCount = Math.Max(1, columnCount);
            return "|" + string.Concat(Enumerable.Repeat(" --- |", safeColumnCount));
        }

        private static bool ContainsInlineFormula(IReadOnlyList<MarkdownInline> inlines)
        {
            return inlines.Any(static inline => inline is MarkdownFormulaInline);
        }

        private static string FlattenInlines(
            IReadOnlyList<MarkdownInline> inlines,
            bool includeFormulaDelimiters,
            bool includeImages)
        {
            var builder = new StringBuilder();

            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case MarkdownTextInline textInline:
                        builder.Append(textInline.Text);
                        break;
                    case MarkdownFormulaInline formulaInline:
                        if (includeFormulaDelimiters)
                        {
                            builder.Append('$').Append(formulaInline.Content).Append('$');
                        }
                        else
                        {
                            builder.Append(formulaInline.Content);
                        }
                        break;
                    case MarkdownImageInline imageInline when includeImages:
                        builder.Append(string.IsNullOrWhiteSpace(imageInline.AltText)
                            ? "[Bild]"
                            : imageInline.AltText);
                        break;
                }
            }

            return builder.ToString();
        }

        private static void AddFormattedText(ICollection<MarkdownInline> inlines, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var cursor = 0;
            foreach (Match match in InlineFormattingRegex.Matches(text))
            {
                if (match.Index > cursor)
                {
                    inlines.Add(new MarkdownTextInline(text.Substring(cursor, match.Index - cursor)));
                }

                if (match.Groups[1].Success)
                {
                    inlines.Add(new MarkdownTextInline(match.Groups[2].Value, isBold: true));
                }
                else if (match.Groups[3].Success)
                {
                    inlines.Add(new MarkdownTextInline(match.Groups[4].Value, isUnderline: true));
                }
                else if (match.Groups[5].Success)
                {
                    inlines.Add(new MarkdownTextInline(match.Groups[6].Value, isHighlight: true));
                }
                else if (match.Groups[7].Success)
                {
                    inlines.Add(new MarkdownTextInline(match.Groups[8].Value, isItalic: true));
                }

                cursor = match.Index + match.Length;
            }

            if (cursor < text.Length)
            {
                inlines.Add(new MarkdownTextInline(text.Substring(cursor)));
            }

            if (inlines.Count == 0)
            {
                inlines.Add(new MarkdownTextInline(text));
            }
        }

        private static ProtectedInlineMatch? GetNextProtectedInlineMatch(string text, int startIndex)
        {
            var imageMatch = InlineImageRegex.Match(text, startIndex);
            var formulaMatch = InlineFormulaRegex.Match(text, startIndex);

            if (!imageMatch.Success && !formulaMatch.Success)
            {
                return null;
            }

            if (imageMatch.Success && (!formulaMatch.Success || imageMatch.Index <= formulaMatch.Index))
            {
                return new ProtectedInlineMatch(imageMatch, ProtectedInlineKind.Image);
            }

            return new ProtectedInlineMatch(formulaMatch, ProtectedInlineKind.Formula);
        }

        private static bool TryParseFormulaBlock(
            IReadOnlyList<string> lines,
            ref int index,
            out MarkdownFormulaBlock formulaBlock)
        {
            var currentLine = lines[index].Trim();
            if (!currentLine.StartsWith("$$", StringComparison.Ordinal))
            {
                formulaBlock = null!;
                return false;
            }

            if (currentLine.Length > 2 && currentLine.EndsWith("$$", StringComparison.Ordinal))
            {
                var singleLineContent = currentLine.Substring(2, currentLine.Length - 4).Trim();
                formulaBlock = new MarkdownFormulaBlock(singleLineContent);
                return true;
            }

            var collectedLines = new List<string>();
            var trailingOpeningContent = currentLine.Substring(2).TrimStart();
            if (!string.IsNullOrEmpty(trailingOpeningContent))
            {
                collectedLines.Add(trailingOpeningContent);
            }

            var foundClosingFence = false;
            for (var cursor = index + 1; cursor < lines.Count; cursor++)
            {
                var candidate = lines[cursor].Trim();
                if (candidate.EndsWith("$$", StringComparison.Ordinal))
                {
                    var beforeClosing = candidate.Substring(0, candidate.Length - 2);
                    if (!string.IsNullOrEmpty(beforeClosing))
                    {
                        collectedLines.Add(beforeClosing);
                    }

                    index = cursor;
                    foundClosingFence = true;
                    break;
                }

                collectedLines.Add(lines[cursor]);
            }

            if (!foundClosingFence)
            {
                formulaBlock = null!;
                return false;
            }

            formulaBlock = new MarkdownFormulaBlock(string.Join("\n", collectedLines).Trim('\n'));
            return true;
        }

        private static bool TryParseTableBlock(
            IReadOnlyList<string> lines,
            ref int index,
            out MarkdownTableBlock tableBlock)
        {
            if (index + 1 >= lines.Count ||
                !IsTableHeaderCandidate(lines[index]) ||
                !TableSeparatorRegex.IsMatch(lines[index + 1]))
            {
                tableBlock = null!;
                return false;
            }

            var header = ParseTableCells(lines[index]);
            var rows = new List<IReadOnlyList<MarkdownTableCell>>();

            var cursor = index + 2;
            while (cursor < lines.Count && IsTableRowCandidate(lines[cursor]))
            {
                rows.Add(ParseTableCells(lines[cursor]));
                cursor++;
            }

            tableBlock = new MarkdownTableBlock(header, rows);
            index = cursor - 1;
            return true;
        }

        private static bool TryParseChecklistBlock(
            IReadOnlyList<string> lines,
            ref int index,
            out MarkdownChecklistBlock checklistBlock)
        {
            var firstMatch = ChecklistRegex.Match(lines[index]);
            if (!firstMatch.Success)
            {
                checklistBlock = null!;
                return false;
            }

            var items = new List<MarkdownChecklistItem>();
            var cursor = index;

            while (cursor < lines.Count)
            {
                var match = ChecklistRegex.Match(lines[cursor]);
                if (!match.Success)
                {
                    break;
                }

                var indentLevel = GetIndentLevel(match.Groups[1].Value);
                var isChecked = string.Equals(match.Groups[2].Value, "x", StringComparison.OrdinalIgnoreCase);
                var inlines = ParseInline(match.Groups[3].Value);

                items.Add(new MarkdownChecklistItem(indentLevel, isChecked, inlines));
                cursor++;
            }

            checklistBlock = new MarkdownChecklistBlock(items);
            index = cursor - 1;
            return true;
        }

        private static bool TryParseQuoteBlock(
            IReadOnlyList<string> lines,
            ref int index,
            out MarkdownQuoteBlock quoteBlock)
        {
            var firstMatch = QuoteRegex.Match(lines[index]);
            if (!firstMatch.Success)
            {
                quoteBlock = null!;
                return false;
            }

            var quoteLines = new List<MarkdownQuoteLine>();
            var cursor = index;

            while (cursor < lines.Count)
            {
                var match = QuoteRegex.Match(lines[cursor]);
                if (!match.Success)
                {
                    break;
                }

                var level = match.Groups[1].Value.Length;
                var inlines = ParseInline(match.Groups[2].Value);
                quoteLines.Add(new MarkdownQuoteLine(level, inlines));
                cursor++;
            }

            quoteBlock = new MarkdownQuoteBlock(quoteLines);
            index = cursor - 1;
            return true;
        }

        private static bool TryParseListBlock(
            IReadOnlyList<string> lines,
            ref int index,
            bool ordered,
            out MarkdownListBlock listBlock)
        {
            var regex = ordered ? OrderedListRegex : UnorderedListRegex;
            var firstMatch = regex.Match(lines[index]);
            if (!firstMatch.Success || (!ordered && ChecklistRegex.IsMatch(lines[index])))
            {
                listBlock = null!;
                return false;
            }

            var items = new List<MarkdownListItem>();
            var cursor = index;

            while (cursor < lines.Count)
            {
                var currentLine = lines[cursor];
                if (!ordered && ChecklistRegex.IsMatch(currentLine))
                {
                    break;
                }

                var match = regex.Match(currentLine);
                if (!match.Success)
                {
                    break;
                }

                var indentLevel = GetIndentLevel(match.Groups[1].Value);
                int? number = null;
                string content;

                if (ordered)
                {
                    number = int.Parse(match.Groups[2].Value);
                    content = match.Groups[3].Value;
                }
                else
                {
                    content = match.Groups[2].Value;
                }

                items.Add(new MarkdownListItem(indentLevel, number, ParseInline(content)));
                cursor++;
            }

            listBlock = new MarkdownListBlock(ordered, items);
            index = cursor - 1;
            return true;
        }

        private static int GetIndentLevel(string indent)
        {
            return indent.Replace("\t", "  ", StringComparison.Ordinal).Length / 2;
        }

        private static bool IsTableHeaderCandidate(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|', StringComparison.Ordinal))
            {
                return false;
            }

            return SplitTableCells(line).Count >= 2;
        }

        private static bool IsTableRowCandidate(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|', StringComparison.Ordinal))
            {
                return false;
            }

            return !TableSeparatorRegex.IsMatch(line);
        }

        private static IReadOnlyList<MarkdownTableCell> ParseTableCells(string line)
        {
            return SplitTableCells(line)
                .Select(static cellText => new MarkdownTableCell(ParseInline(cellText)))
                .ToArray();
        }

        private static List<string> SplitTableCells(string line)
        {
            var row = line.Trim();
            if (row.StartsWith("|", StringComparison.Ordinal))
            {
                row = row.Substring(1);
            }

            if (row.EndsWith("|", StringComparison.Ordinal))
            {
                row = row.Substring(0, row.Length - 1);
            }

            var cells = new List<string>();
            var currentCell = new StringBuilder();
            var insideInlineFormula = false;

            for (var index = 0; index < row.Length; index++)
            {
                var character = row[index];

                if (character == '\\')
                {
                    if (index + 1 < row.Length)
                    {
                        var nextCharacter = row[index + 1];
                        if (nextCharacter is '|' or '$' or '\\')
                        {
                            currentCell.Append(nextCharacter);
                            index++;
                            continue;
                        }
                    }

                    currentCell.Append('\\');
                    continue;
                }

                if (character == '$')
                {
                    if (index + 1 < row.Length && row[index + 1] == '$')
                    {
                        currentCell.Append("$$");
                        index++;
                        continue;
                    }

                    insideInlineFormula = !insideInlineFormula;
                    currentCell.Append(character);
                    continue;
                }

                if (character == '|' && !insideInlineFormula)
                {
                    cells.Add(currentCell.ToString().Trim());
                    currentCell.Clear();
                    continue;
                }

                currentCell.Append(character);
            }

            cells.Add(currentCell.ToString().Trim());
            return cells;
        }

        private static string StripOuterCodeFence(string markdown)
        {
            var lines = markdown.Split('\n');
            var start = 0;
            var end = lines.Length - 1;

            while (start <= end && string.IsNullOrWhiteSpace(lines[start]))
            {
                start++;
            }

            while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            {
                end--;
            }

            if (start >= end)
            {
                return markdown;
            }

            if (!FenceStartRegex.IsMatch(lines[start]) || !FenceEndRegex.IsMatch(lines[end]))
            {
                return markdown;
            }

            var innerLineCount = end - start - 1;
            if (innerLineCount <= 0)
            {
                return string.Empty;
            }

            var innerLines = new string[innerLineCount];
            Array.Copy(lines, start + 1, innerLines, 0, innerLineCount);
            return string.Join("\n", innerLines);
        }

        private enum ProtectedInlineKind
        {
            Image,
            Formula
        }

        private readonly struct ProtectedInlineMatch
        {
            public ProtectedInlineMatch(Match match, ProtectedInlineKind kind)
            {
                Match = match;
                Kind = kind;
            }

            public Match Match { get; }
            public ProtectedInlineKind Kind { get; }
        }

        public sealed class MarkdownDocument
        {
            public MarkdownDocument(IReadOnlyList<MarkdownBlock> blocks)
            {
                Blocks = blocks;
            }

            public IReadOnlyList<MarkdownBlock> Blocks { get; }
        }

        public abstract class MarkdownBlock
        {
        }

        public sealed class MarkdownBlankLineBlock : MarkdownBlock
        {
        }

        public sealed class MarkdownParagraphBlock : MarkdownBlock
        {
            public MarkdownParagraphBlock(IReadOnlyList<MarkdownInline> inlines)
            {
                Inlines = inlines;
            }

            public IReadOnlyList<MarkdownInline> Inlines { get; }
        }

        public sealed class MarkdownListBlock : MarkdownBlock
        {
            public MarkdownListBlock(bool isOrdered, IReadOnlyList<MarkdownListItem> items)
            {
                IsOrdered = isOrdered;
                Items = items;
            }

            public bool IsOrdered { get; }
            public IReadOnlyList<MarkdownListItem> Items { get; }
        }

        public sealed class MarkdownChecklistBlock : MarkdownBlock
        {
            public MarkdownChecklistBlock(IReadOnlyList<MarkdownChecklistItem> items)
            {
                Items = items;
            }

            public IReadOnlyList<MarkdownChecklistItem> Items { get; }
        }

        public sealed class MarkdownQuoteBlock : MarkdownBlock
        {
            public MarkdownQuoteBlock(IReadOnlyList<MarkdownQuoteLine> lines)
            {
                Lines = lines;
            }

            public IReadOnlyList<MarkdownQuoteLine> Lines { get; }
        }

        public sealed class MarkdownTableBlock : MarkdownBlock
        {
            public MarkdownTableBlock(
                IReadOnlyList<MarkdownTableCell> header,
                IReadOnlyList<IReadOnlyList<MarkdownTableCell>> rows)
            {
                Header = header;
                Rows = rows;
            }

            public IReadOnlyList<MarkdownTableCell> Header { get; }
            public IReadOnlyList<IReadOnlyList<MarkdownTableCell>> Rows { get; }
        }

        public sealed class MarkdownFormulaBlock : MarkdownBlock
        {
            public MarkdownFormulaBlock(string content)
            {
                Content = content;
            }

            public string Content { get; }
        }

        public sealed class MarkdownListItem
        {
            public MarkdownListItem(int indentLevel, int? number, IReadOnlyList<MarkdownInline> inlines)
            {
                IndentLevel = indentLevel;
                Number = number;
                Inlines = inlines;
            }

            public int IndentLevel { get; }
            public int? Number { get; }
            public IReadOnlyList<MarkdownInline> Inlines { get; }
        }

        public sealed class MarkdownChecklistItem
        {
            public MarkdownChecklistItem(int indentLevel, bool isChecked, IReadOnlyList<MarkdownInline> inlines)
            {
                IndentLevel = indentLevel;
                IsChecked = isChecked;
                Inlines = inlines;
            }

            public int IndentLevel { get; }
            public bool IsChecked { get; }
            public IReadOnlyList<MarkdownInline> Inlines { get; }
        }

        public sealed class MarkdownQuoteLine
        {
            public MarkdownQuoteLine(int level, IReadOnlyList<MarkdownInline> inlines)
            {
                Level = level;
                Inlines = inlines;
            }

            public int Level { get; }
            public IReadOnlyList<MarkdownInline> Inlines { get; }
        }

        public sealed class MarkdownTableCell
        {
            public MarkdownTableCell(IReadOnlyList<MarkdownInline> inlines)
            {
                Inlines = inlines;
            }

            public IReadOnlyList<MarkdownInline> Inlines { get; }
        }

        public abstract class MarkdownInline
        {
        }

        public sealed class MarkdownTextInline : MarkdownInline
        {
            public MarkdownTextInline(
                string text,
                bool isBold = false,
                bool isItalic = false,
                bool isUnderline = false,
                bool isHighlight = false)
            {
                Text = text;
                IsBold = isBold;
                IsItalic = isItalic;
                IsUnderline = isUnderline;
                IsHighlight = isHighlight;
            }

            public string Text { get; }
            public bool IsBold { get; }
            public bool IsItalic { get; }
            public bool IsUnderline { get; }
            public bool IsHighlight { get; }
        }

        public sealed class MarkdownImageInline : MarkdownInline
        {
            public MarkdownImageInline(string altText, string source)
            {
                AltText = altText;
                Source = source;
            }

            public string AltText { get; }
            public string Source { get; }
        }

        public sealed class MarkdownFormulaInline : MarkdownInline
        {
            public MarkdownFormulaInline(string content)
            {
                Content = content;
            }

            public string Content { get; }
        }
    }
}
