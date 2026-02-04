using System;
using System.Collections.Generic;

namespace CapyCard.Models
{
    public enum TextIssueKind
    {
        Spelling
    }

    public sealed class TextIssue
    {
        public TextIssue(int start, int length, string word, IReadOnlyList<string> suggestions, TextIssueKind kind)
        {
            Start = start;
            Length = length;
            Word = word ?? string.Empty;
            Suggestions = suggestions ?? Array.Empty<string>();
            Kind = kind;
        }

        public int Start { get; }
        public int Length { get; }
        public string Word { get; }
        public IReadOnlyList<string> Suggestions { get; }
        public TextIssueKind Kind { get; }
    }
}
