using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Threading;
using CapyCard.Models;
using WeCantSpell.Hunspell;

namespace CapyCard.Services.TextChecking
{
    public sealed class HunspellSpellCheckService : ITextCheckingService
    {
        private static readonly Lazy<Task<WordList>> SharedWordList = new(LoadDefaultWordListAsync);
        private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled);
        private static readonly Regex FullWordRegex = new(@"^\p{L}+$", RegexOptions.Compiled);
        private static readonly Regex ImagePlaceholderRegex = new(@"!\[[^\]]*\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly string UserDictionaryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CapyCard",
            "Spellcheck");
        private const string SupportedLocale = "de-DE";
        private const int MaxSuggestions = 5;
        private readonly Lazy<Task<WordList>> _wordList;
        private readonly HashSet<string> _userWords = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _userWordsLock = new(1, 1);
        private bool _userWordsLoaded;

        public HunspellSpellCheckService()
            : this(SharedWordList)
        {
        }

        public HunspellSpellCheckService(Func<Task<WordList>> wordListFactory)
            : this(new Lazy<Task<WordList>>(wordListFactory))
        {
        }

        private HunspellSpellCheckService(Lazy<Task<WordList>> wordList)
        {
            _wordList = wordList;
        }

        public async Task<IReadOnlyList<TextIssue>> CheckAsync(string text, string locale, CancellationToken ct)
        {
            if (!IsSupportedLocale(locale))
            {
                return Array.Empty<TextIssue>();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<TextIssue>();
            }

            await EnsureUserWordsLoadedAsync(locale, ct);

            var ignoredRanges = GetIgnoredRanges(text);
            var issues = new List<TextIssue>();
            var wordList = await _wordList.Value.ConfigureAwait(false);

            foreach (Match match in WordRegex.Matches(text))
            {
                if (ct.IsCancellationRequested)
                {
                    return issues;
                }

                if (IsInIgnoredRange(ignoredRanges, match.Index, match.Length))
                {
                    continue;
                }

                var word = match.Value;
                if (_userWords.Contains(word))
                {
                    continue;
                }

                if (wordList.Check(word))
                {
                    continue;
                }

                var suggestions = wordList.Suggest(word)
                    .Take(MaxSuggestions)
                    .ToArray();

                issues.Add(new TextIssue(match.Index, match.Length, word, suggestions, TextIssueKind.Spelling));
            }

            return issues;
        }

        public async Task<bool> AddWordAsync(string word, string locale, CancellationToken ct)
        {
            if (!IsSupportedLocale(locale))
            {
                return false;
            }

            var normalized = NormalizeWord(word);
            if (string.IsNullOrEmpty(normalized) || !FullWordRegex.IsMatch(normalized))
            {
                return false;
            }

            await EnsureUserWordsLoadedAsync(locale, ct);

            await _userWordsLock.WaitAsync(ct);
            try
            {
                if (_userWords.Contains(normalized))
                {
                    return false;
                }

                _userWords.Add(normalized);

                Directory.CreateDirectory(UserDictionaryDirectory);
                var path = GetUserDictionaryPath(locale);
                await File.AppendAllTextAsync(path, normalized + Environment.NewLine, Encoding.UTF8, ct);
                return true;
            }
            finally
            {
                _userWordsLock.Release();
            }
        }

        private static async Task<WordList> LoadDefaultWordListAsync()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var (dicBytes, affBytes) = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var affUri = new Uri("avares://CapyCard/Resources/Spellcheck/de_DE.aff");
                var dicUri = new Uri("avares://CapyCard/Resources/Spellcheck/de_DE.dic");

                using var affStream = AssetLoader.Open(affUri);
                using var dicStream = AssetLoader.Open(dicUri);
                using var affBuffer = new MemoryStream();
                using var dicBuffer = new MemoryStream();
                affStream.CopyTo(affBuffer);
                dicStream.CopyTo(dicBuffer);
                return (dicBuffer.ToArray(), affBuffer.ToArray());
            });

            var wordList = await Task.Run(() =>
            {
                using var dicStream = new MemoryStream(dicBytes, writable: false);
                using var affStream = new MemoryStream(affBytes, writable: false);
                return WordList.CreateFromStreams(dicStream, affStream);
            }).ConfigureAwait(false);

            return wordList;
        }

        private static bool IsSupportedLocale(string locale)
        {
            return string.Equals(locale, SupportedLocale, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWord(string word)
        {
            return (word ?? string.Empty).Trim();
        }

        private static string GetUserDictionaryPath(string locale)
        {
            return Path.Combine(UserDictionaryDirectory, $"user-words.{locale}.txt");
        }

        private async Task EnsureUserWordsLoadedAsync(string locale, CancellationToken ct)
        {
            if (_userWordsLoaded || !IsSupportedLocale(locale))
            {
                return;
            }

            await _userWordsLock.WaitAsync(ct);
            try
            {
                if (_userWordsLoaded)
                {
                    return;
                }

                var path = GetUserDictionaryPath(locale);
                if (File.Exists(path))
                {
                    var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, ct);
                    foreach (var line in lines)
                    {
                        var normalized = NormalizeWord(line);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            _userWords.Add(normalized);
                        }
                    }
                }

                _userWordsLoaded = true;
            }
            finally
            {
                _userWordsLock.Release();
            }
        }

        private static List<(int Start, int End)> GetIgnoredRanges(string text)
        {
            var ranges = new List<(int Start, int End)>();
            foreach (Match match in ImagePlaceholderRegex.Matches(text))
            {
                ranges.Add((match.Index, match.Index + match.Length));
            }

            return ranges;
        }

        private static bool IsInIgnoredRange(List<(int Start, int End)> ranges, int start, int length)
        {
            if (ranges.Count == 0)
            {
                return false;
            }

            var end = start + length;
            foreach (var range in ranges)
            {
                if (start >= range.Start && end <= range.End)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
