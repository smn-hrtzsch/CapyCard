using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.Services.ImportExport.Formats
{
    /// <summary>
    /// Handler für CSV-Import und -Export.
    /// Export: UTF-8 mit BOM, Semikolon als Trennzeichen.
    /// Import: Automatische Erkennung von Trennzeichen und Header.
    /// </summary>
    public class CsvFormatHandler : IFormatHandler
    {
        public string[] SupportedExtensions => new[] { ".csv", ".tsv", ".txt" };
        public string FormatName => "CSV";
        public string FormatDescription => "Einfaches Tabellenformat. Öffne in Excel oder Google Sheets. Bilder werden als Platzhalter exportiert.";
        public bool IsAvailable => true;

        // Regex für Markdown-Bilder: ![alt](data:...)
        private static readonly Regex ImageRegex = new(@"!\[([^\]]*)\]\(data:[^)]+\)", RegexOptions.Compiled);

        /// <inheritdoc/>
        public async Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    return ImportPreview.Failed("Datei ist leer.");
                }

                var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                {
                    return ImportPreview.Failed("Keine Zeilen in der Datei gefunden.");
                }

                var delimiter = DetectDelimiter(lines[0]);
                var hasHeader = DetectHeader(lines[0], delimiter);
                var dataLines = hasHeader ? lines.Skip(1).ToArray() : lines;

                var cardCount = dataLines.Count(line => 
                    ParseLine(line, delimiter).Length >= 2);

                var preview = ImportPreview.Successful(FormatName, Path.GetFileNameWithoutExtension(fileName), cardCount, 0);
                preview.HasProgress = hasHeader && lines[0].Contains("BoxIndex", StringComparison.OrdinalIgnoreCase);
                preview.HasMedia = false; // CSV unterstützt keine eingebetteten Bilder

                return preview;
            }
            catch (Exception ex)
            {
                return ImportPreview.Failed($"Fehler beim Lesen der CSV: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync();

                var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                {
                    return ImportResult.Failed("Keine Zeilen in der Datei gefunden.");
                }

                var delimiter = DetectDelimiter(lines[0]);
                var hasHeader = DetectHeader(lines[0], delimiter);
                var headers = hasHeader ? ParseLine(lines[0], delimiter) : null;
                var dataLines = hasHeader ? lines.Skip(1).ToArray() : lines;

                // Spalten-Indizes ermitteln
                int frontIdx = 0, backIdx = 1, deckIdx = -1, subDeckIdx = -1, boxIdx = -1, lastReviewedIdx = -1;

                if (headers != null)
                {
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var h = headers[i].ToLowerInvariant().Trim();
                        if (h == "front" || h == "vorderseite" || h == "frage") frontIdx = i;
                        else if (h == "back" || h == "rückseite" || h == "antwort") backIdx = i;
                        else if (h == "deck" || h == "fach") deckIdx = i;
                        else if (h == "subdeck" || h == "thema") subDeckIdx = i;
                        else if (h == "boxindex" || h == "box") boxIdx = i;
                        else if (h == "lastreviewed" || h == "letztegelernt") lastReviewedIdx = i;
                    }
                }

                using var context = new FlashcardDbContext();

                // Ziel-Deck ermitteln oder erstellen
                Deck targetDeck;
                int subDecksCreated = 0;

                switch (options.Target)
                {
                    case ImportTarget.NewDeck:
                        var deckName = options.NewDeckName ?? Path.GetFileNameWithoutExtension(fileName);
                        targetDeck = new Deck { Name = deckName };
                        context.Decks.Add(targetDeck);
                        await context.SaveChangesAsync();

                        var defaultSubDeck = new Deck
                        {
                            Name = "Allgemein",
                            ParentDeckId = targetDeck.Id,
                            IsDefault = true
                        };
                        context.Decks.Add(defaultSubDeck);
                        await context.SaveChangesAsync();
                        subDecksCreated = 1;
                        break;

                    case ImportTarget.ExistingDeck:
                        if (!options.TargetDeckId.HasValue)
                        {
                            return ImportResult.Failed("Kein Ziel-Fach ausgewählt.");
                        }
                        targetDeck = await context.Decks
                            .Include(d => d.SubDecks)
                            .FirstOrDefaultAsync(d => d.Id == options.TargetDeckId.Value);
                        if (targetDeck == null)
                        {
                            return ImportResult.Failed("Ziel-Fach nicht gefunden.");
                        }
                        break;

                    default:
                        return ImportResult.Failed("CSV-Import unterstützt nur 'Neues Fach' oder 'In bestehendes Fach'.");
                }

                // Karten importieren
                int imported = 0, skipped = 0, updated = 0;
                var warnings = new List<string>();

                // SubDeck-Cache für Performance
                var subDeckCache = targetDeck.SubDecks.ToDictionary(sd => sd.Name.ToLowerInvariant(), sd => sd);
                var defaultDeck = targetDeck.SubDecks.FirstOrDefault(sd => sd.IsDefault);

                foreach (var line in dataLines)
                {
                    var fields = ParseLine(line, delimiter);
                    if (fields.Length < 2)
                    {
                        warnings.Add($"Zeile übersprungen (weniger als 2 Spalten): {line.Substring(0, Math.Min(50, line.Length))}...");
                        continue;
                    }

                    var front = fields.Length > frontIdx ? fields[frontIdx].Trim() : "";
                    var back = fields.Length > backIdx ? fields[backIdx].Trim() : "";

                    if (string.IsNullOrWhiteSpace(front))
                    {
                        warnings.Add($"Zeile übersprungen (leere Vorderseite): {line.Substring(0, Math.Min(50, line.Length))}...");
                        continue;
                    }

                    // Ziel-SubDeck ermitteln
                    Deck cardDeck = defaultDeck!;
                    if (subDeckIdx >= 0 && fields.Length > subDeckIdx && !string.IsNullOrWhiteSpace(fields[subDeckIdx]))
                    {
                        var subDeckName = fields[subDeckIdx].Trim();
                        var subDeckKey = subDeckName.ToLowerInvariant();

                        if (!subDeckCache.TryGetValue(subDeckKey, out var existingSubDeck))
                        {
                            existingSubDeck = new Deck
                            {
                                Name = subDeckName,
                                ParentDeckId = targetDeck.Id
                            };
                            context.Decks.Add(existingSubDeck);
                            await context.SaveChangesAsync();
                            subDeckCache[subDeckKey] = existingSubDeck;
                            subDecksCreated++;
                        }
                        cardDeck = existingSubDeck;
                    }

                    // Duplikat-Prüfung
                    var existingCard = await context.Cards.FirstOrDefaultAsync(c =>
                        c.DeckId == cardDeck.Id && c.Front == front);

                    if (existingCard != null)
                    {
                        switch (options.OnDuplicate)
                        {
                            case DuplicateHandling.Skip:
                                skipped++;
                                continue;

                            case DuplicateHandling.Replace:
                                existingCard.Back = back;
                                updated++;
                                break;

                            case DuplicateHandling.KeepBoth:
                                // Fall through
                                break;
                        }
                    }

                    if (existingCard == null || options.OnDuplicate == DuplicateHandling.KeepBoth)
                    {
                        var card = new Card
                        {
                            Front = front,
                            Back = back,
                            DeckId = cardDeck.Id
                        };
                        context.Cards.Add(card);
                        await context.SaveChangesAsync();

                        // Lernfortschritt importieren (falls vorhanden)
                        if (options.IncludeProgress && boxIdx >= 0 && fields.Length > boxIdx)
                        {
                            if (int.TryParse(fields[boxIdx], out var boxIndex))
                            {
                                var score = new CardSmartScore
                                {
                                    CardId = card.Id,
                                    BoxIndex = Math.Clamp(boxIndex, 0, 5),
                                    Score = boxIndex * 0.2, // Approximation
                                    LastReviewed = DateTime.UtcNow
                                };

                                if (lastReviewedIdx >= 0 && fields.Length > lastReviewedIdx &&
                                    DateTime.TryParse(fields[lastReviewedIdx], CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastReviewed))
                                {
                                    score.LastReviewed = lastReviewed;
                                }

                                context.CardSmartScores.Add(score);
                                await context.SaveChangesAsync();
                            }
                        }

                        imported++;
                    }
                }

                await context.SaveChangesAsync();

                var result = ImportResult.Successful(imported, subDecksCreated, targetDeck.Id);
                result.CardsSkipped = skipped;
                result.CardsUpdated = updated;
                result.Warnings = warnings;
                return result;
            }
            catch (Exception ex)
            {
                return ImportResult.Failed($"Fehler beim CSV-Import: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ExportResult> ExportAsync(Stream stream, ExportOptions options)
        {
            try
            {
                using var context = new FlashcardDbContext();

                var deck = await context.Decks
                    .Include(d => d.SubDecks)
                        .ThenInclude(sd => sd.Cards)
                    .Include(d => d.Cards)
                    .FirstOrDefaultAsync(d => d.Id == options.DeckId);

                if (deck == null)
                {
                    return ExportResult.Failed("Fach nicht gefunden.");
                }

                // UTF-8 BOM für Excel-Kompatibilität
                var bom = new byte[] { 0xEF, 0xBB, 0xBF };
                await stream.WriteAsync(bom);

                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                // Header schreiben
                var headerParts = new List<string> { "Front", "Back", "Deck", "SubDeck" };
                if (options.IncludeProgress)
                {
                    headerParts.AddRange(new[] { "BoxIndex", "LastReviewed" });
                }
                await writer.WriteLineAsync(string.Join(";", headerParts));

                int cardCount = 0;
                int subDeckCount = 0;
                int imageCounter = 1;

                // Karten exportieren
                async Task ExportCards(IEnumerable<Card> cards, string deckName, string subDeckName)
                {
                    foreach (var card in cards)
                    {
                        var front = ConvertImagesForCsv(card.Front, ref imageCounter);
                        var back = ConvertImagesForCsv(card.Back, ref imageCounter);

                        var parts = new List<string>
                        {
                            EscapeCsvField(front),
                            EscapeCsvField(back),
                            EscapeCsvField(deckName),
                            EscapeCsvField(subDeckName)
                        };

                        if (options.IncludeProgress)
                        {
                            var score = await context.CardSmartScores.FirstOrDefaultAsync(s => s.CardId == card.Id);
                            parts.Add(score?.BoxIndex.ToString() ?? "0");
                            parts.Add(score?.LastReviewed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
                        }

                        await writer.WriteLineAsync(string.Join(";", parts));
                        cardCount++;
                    }
                }

                // Karten sammeln basierend auf Scope
                IEnumerable<Deck> subDecksToExport;

                if (options.Scope == ExportScope.SelectedCards && options.SelectedCardIds?.Count > 0)
                {
                    // Nur ausgewählte Karten
                    var selectedCards = await context.Cards
                        .Include(c => c.Deck)
                        .Where(c => options.SelectedCardIds.Contains(c.Id))
                        .ToListAsync();

                    foreach (var card in selectedCards)
                    {
                        var subDeckName = card.Deck.ParentDeckId.HasValue ? card.Deck.Name : "Allgemein";
                        var deckName = card.Deck.ParentDeckId.HasValue
                            ? (await context.Decks.FindAsync(card.Deck.ParentDeckId))?.Name ?? deck.Name
                            : deck.Name;

                        await ExportCards(new[] { card }, deckName, subDeckName);
                    }
                }
                else
                {
                    if (options.Scope == ExportScope.SelectedSubDecks && options.SelectedSubDeckIds?.Count > 0)
                    {
                        subDecksToExport = deck.SubDecks.Where(sd => options.SelectedSubDeckIds.Contains(sd.Id));
                    }
                    else
                    {
                        subDecksToExport = deck.SubDecks;
                    }

                    foreach (var subDeck in subDecksToExport)
                    {
                        await ExportCards(subDeck.Cards, deck.Name, subDeck.Name);
                        subDeckCount++;
                    }
                }

                await writer.FlushAsync();
                return ExportResult.SuccessfulWithData(Array.Empty<byte>(), cardCount, subDeckCount);
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"Fehler beim CSV-Export: {ex.Message}");
            }
        }

        #region Private Helper Methods

        private static char DetectDelimiter(string line)
        {
            // Zähle die Vorkommen verschiedener Trennzeichen
            var delimiters = new[] { ';', ',', '\t' };
            var counts = delimiters.Select(d => (d, count: line.Count(c => c == d))).ToArray();

            // Wähle das Trennzeichen mit den meisten Vorkommen
            var best = counts.OrderByDescending(x => x.count).First();
            return best.count > 0 ? best.d : ';';
        }

        private static bool DetectHeader(string line, char delimiter)
        {
            var fields = ParseLine(line, delimiter);
            if (fields.Length < 2) return false;

            // Prüfe auf typische Header-Bezeichnungen
            var headerWords = new[] { "front", "back", "vorderseite", "rückseite", "frage", "antwort", "deck", "subdeck", "thema", "boxindex" };
            return fields.Any(f => headerWords.Contains(f.ToLowerInvariant().Trim()));
        }

        private static string[] ParseLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            // Escape quotes and wrap in quotes if necessary
            if (value.Contains('"') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private static string ConvertImagesForCsv(string text, ref int imageCounter)
        {
            // Ersetze Markdown-Bilder durch Platzhalter
            var counter = imageCounter;
            var result = ImageRegex.Replace(text, match =>
            {
                var placeholder = $"[Bild: {counter}]";
                counter++;
                return placeholder;
            });
            imageCounter = counter;
            return result;
        }

        #endregion
    }
}
