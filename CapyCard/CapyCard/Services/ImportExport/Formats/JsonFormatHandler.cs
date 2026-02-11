using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.Services.ImportExport.Formats
{
    /// <summary>
    /// Handler für LLM-basierten JSON-Import.
    /// Unterstützt das Einlesen von JSON-Text, der direkt von KI-Modellen generiert wurde.
    /// Robust gegenüber Markdown-Code-Fences.
    /// </summary>
    public class JsonFormatHandler : IFormatHandler
    {
        public string[] SupportedExtensions => new[] { ".json", ".txt" };
        public string FormatName => "KI / JSON";
        public string FormatDescription => "Importiere Karten direkt aus KI-generiertem Text. Unterstützt ein Haupt-Deck mit einem SubDeck-Level und Bilder via Base64.";
        public bool IsAvailable => true;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <inheritdoc/>
        public async Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName)
        {
            string jsonContent = string.Empty;
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var rawContent = await reader.ReadToEndAsync();
                jsonContent = CleanJson(rawContent);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return ImportPreview.Failed("Kein gültiger JSON-Inhalt gefunden.");
                }

                var data = JsonSerializer.Deserialize<JsonDeck>(jsonContent, JsonOptions);
                if (data == null)
                {
                    return ImportPreview.Failed("JSON konnte nicht in das erwartete Format umgewandelt werden.");
                }

                var normalizationWarnings = new List<string>();
                NormalizeDeckForSingleLevel(data, depth: 0, normalizationWarnings);

                var cardCount = CountCards(data);
                var subDeckCount = CountSubDecks(data);
                var hasMedia = HasEmbeddedImages(data);

                var preview = ImportPreview.Successful(FormatName, data.Name ?? Path.GetFileNameWithoutExtension(fileName), cardCount, subDeckCount);
                preview.HasMedia = hasMedia;
                preview.HasProgress = false; // LLM Import hat normalerweise keinen Lernfortschritt

                return preview;
            }
            catch (JsonException jex)
            {
                var previewText = jsonContent.Length > 30 ? jsonContent.Substring(0, 30) + "..." : jsonContent;
                return ImportPreview.Failed($"Ungültiges JSON-Format: {jex.Message} (Start des Inhalts: '{previewText}')");
            }
            catch (Exception ex)
            {
                return ImportPreview.Failed($"Fehler beim Analysieren des JSON: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var rawContent = await reader.ReadToEndAsync();
                var jsonContent = CleanJson(rawContent);

                var data = JsonSerializer.Deserialize<JsonDeck>(jsonContent, JsonOptions);
                if (data == null)
                {
                    return ImportResult.Failed("JSON konnte nicht deserialisiert werden.");
                }

                var warnings = new List<string>();
                NormalizeDeckForSingleLevel(data, depth: 0, warnings);

                using var context = new FlashcardDbContext();

                Deck targetDeck;
                int subDecksCreated = 0;

                switch (options.Target)
                {
                    case ImportTarget.NewDeck:
                        var deckName = options.NewDeckName ?? data.Name ?? Path.GetFileNameWithoutExtension(fileName);
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
                        var foundDeck = await context.Decks
                            .Include(d => d.SubDecks)
                            .FirstOrDefaultAsync(d => d.Id == options.TargetDeckId.Value);
                        
                        if (foundDeck == null)
                        {
                            return ImportResult.Failed("Ziel-Fach nicht gefunden.");
                        }
                        targetDeck = foundDeck;
                        break;

                    default:
                        return ImportResult.Failed("JSON-Import unterstützt nur 'Neues Fach' oder 'In bestehendes Fach'.");
                }

                var (imported, skipped, updated, additionalSubDecks) = await ImportJsonDeckAsync(
                    context,
                    data,
                    targetDeck,
                    options,
                    depth: 0,
                    warnings);
                subDecksCreated += additionalSubDecks;

                var result = ImportResult.Successful(imported, subDecksCreated, targetDeck.Id);
                result.CardsSkipped = skipped;
                result.CardsUpdated = updated;
                result.Warnings = warnings;
                return result;
            }
            catch (Exception ex)
            {
                return ImportResult.Failed($"Fehler beim JSON-Import: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public Task<ExportResult> ExportAsync(Stream stream, ExportOptions options)
        {
            // LLM Import Handler unterstützt keinen Export in dieses spezifische Format
            return Task.FromResult(ExportResult.Failed("JSON-Export wird von diesem Handler nicht unterstützt."));
        }

        #region Helper Methods

        private string CleanJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            var trimmed = content.Trim();

            // 1. Wenn es bereits mit { oder [ anfängt und endet, ist es wahrscheinlich direktes JSON
            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || 
                (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return trimmed;
            }

            // 2. Suche nach expliziten JSON-Markdown-Fences (```json ... ```)
            var jsonMatch = Regex.Match(content, @"```json\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (jsonMatch.Success)
            {
                var code = jsonMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(code)) return code;
            }

            // 3. Suche nach den äußersten Klammern
            var firstBrace = content.IndexOfAny(new[] { '{', '[' });
            var lastBrace = content.LastIndexOfAny(new[] { '}', ']' });

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                var json = content.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                return json;
            }

            return trimmed;
        }

        private int CountCards(JsonDeck deck)
        {
            int count = deck.Cards?.Count(IsValidCard) ?? 0;
            if (deck.SubDecks != null)
            {
                count += deck.SubDecks.Sum(CountCards);
            }
            return count;
        }

        private int CountSubDecks(JsonDeck deck)
        {
            return deck.SubDecks?.Count(sd => !string.IsNullOrWhiteSpace(sd.Name) && HasAnyValidContent(sd)) ?? 0;
        }

        private bool HasEmbeddedImages(JsonDeck deck)
        {
            bool hasImages = false;
            if (deck.Cards != null)
            {
                hasImages = deck.Cards.Any(c => 
                    (c.Front != null && c.Front.Contains("data:image")) || 
                    (c.Back != null && c.Back.Contains("data:image")));
            }

            if (!hasImages && deck.SubDecks != null)
            {
                hasImages = deck.SubDecks.Any(HasEmbeddedImages);
            }

            return hasImages;
        }

        private static bool IsValidCard(JsonCard? card)
        {
            return card != null && !string.IsNullOrWhiteSpace(card.Front);
        }

        private static bool HasAnyValidContent(JsonDeck? deck)
        {
            if (deck == null)
            {
                return false;
            }

            var hasCards = deck.Cards?.Any(IsValidCard) == true;
            var hasSubDecks = deck.SubDecks?.Any(static child => !string.IsNullOrWhiteSpace(child.Name) && HasAnyValidContent(child)) == true;
            return hasCards || hasSubDecks;
        }

        private void NormalizeDeckForSingleLevel(JsonDeck deck, int depth, List<string> warnings)
        {
            deck.Name = deck.Name?.Trim();

            var normalizedCards = new List<JsonCard>();
            if (deck.Cards != null)
            {
                foreach (var card in deck.Cards)
                {
                    if (!IsValidCard(card))
                    {
                        continue;
                    }

                    normalizedCards.Add(new JsonCard
                    {
                        Front = card!.Front!.Trim(),
                        Back = card.Back?.Trim() ?? string.Empty
                    });
                }
            }

            deck.Cards = normalizedCards;

            var subDecks = deck.SubDecks ?? new List<JsonDeck>();
            if (subDecks.Count == 0)
            {
                deck.SubDecks = new List<JsonDeck>();
                return;
            }

            if (depth >= 1)
            {
                var flattenedCards = CollectNestedCards(subDecks);
                if (flattenedCards.Count > 0)
                {
                    deck.Cards.AddRange(flattenedCards);
                }

                AddWarningOnce(
                    warnings,
                    "Verschachtelte subDecks wurden ignoriert. CapyCard unterstützt beim JSON-Import nur ein SubDeck-Level.");

                deck.SubDecks = new List<JsonDeck>();
                return;
            }

            var normalizedSubDecks = new List<JsonDeck>();
            foreach (var subDeck in subDecks)
            {
                if (subDeck == null)
                {
                    continue;
                }

                NormalizeDeckForSingleLevel(subDeck, depth + 1, warnings);

                if (string.IsNullOrWhiteSpace(subDeck.Name))
                {
                    if (HasAnyValidContent(subDeck))
                    {
                        AddWarningOnce(warnings, "Ein Unterthema ohne Namen wurde beim Import ignoriert.");
                    }

                    continue;
                }

                if (!HasAnyValidContent(subDeck))
                {
                    continue;
                }

                normalizedSubDecks.Add(subDeck);
            }

            deck.SubDecks = normalizedSubDecks;
        }

        private static List<JsonCard> CollectNestedCards(IEnumerable<JsonDeck> nestedDecks)
        {
            var cards = new List<JsonCard>();

            foreach (var deck in nestedDecks)
            {
                if (deck.Cards != null)
                {
                    foreach (var card in deck.Cards)
                    {
                        if (!IsValidCard(card))
                        {
                            continue;
                        }

                        cards.Add(new JsonCard
                        {
                            Front = card!.Front!.Trim(),
                            Back = card.Back?.Trim() ?? string.Empty
                        });
                    }
                }

                if (deck.SubDecks != null && deck.SubDecks.Count > 0)
                {
                    cards.AddRange(CollectNestedCards(deck.SubDecks));
                }
            }

            return cards;
        }

        private static void AddWarningOnce(ICollection<string> warnings, string warning)
        {
            if (!warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }

        private async Task<(int imported, int skipped, int updated, int subDecksCreated)> ImportJsonDeckAsync(
            FlashcardDbContext context,
            JsonDeck data,
            Deck targetDeck,
            ImportOptions options,
            int depth,
            ICollection<string> warnings)
        {
            int imported = 0, skipped = 0, updated = 0, subDecksCreated = 0;

            // Sicherstellen, dass die Liste initialisiert ist
            targetDeck.SubDecks ??= new List<Deck>();

            // Bestimme das Ziel-Deck für die Karten in diesem Objekt
            int cardTargetDeckId;

            if (targetDeck.ParentDeckId == null)
            {
                // Wir sind im Haupt-Fach -> Karten brauchen ein Thema (Allgemein)
                var defaultSubDeck = targetDeck.SubDecks.FirstOrDefault(sd => sd.IsDefault);
                if (defaultSubDeck == null)
                {
                    defaultSubDeck = new Deck
                    {
                        Name = "Allgemein",
                        ParentDeckId = targetDeck.Id,
                        IsDefault = true
                    };
                    context.Decks.Add(defaultSubDeck);
                    await context.SaveChangesAsync();
                    targetDeck.SubDecks.Add(defaultSubDeck);
                    subDecksCreated++;
                }
                cardTargetDeckId = defaultSubDeck.Id;
            }
            else
            {
                // Wir sind bereits in einem Unter-Thema -> Karten direkt hier rein
                cardTargetDeckId = targetDeck.Id;
            }

            // Karten importieren
            if (data.Cards != null)
            {
                foreach (var cardData in data.Cards)
                {
                    if (string.IsNullOrWhiteSpace(cardData.Front)) continue;

                    var result = await ImportCardAsync(context, cardData, cardTargetDeckId, options);
                    imported += result.imported;
                    skipped += result.skipped;
                    updated += result.updated;
                }
            }

            // SubDecks importieren
            if (data.SubDecks != null && data.SubDecks.Count > 0)
            {
                if (depth >= 1)
                {
                    AddWarningOnce(
                        warnings,
                        "Verschachtelte subDecks wurden ignoriert. CapyCard unterstützt beim JSON-Import nur ein SubDeck-Level.");

                    return (imported, skipped, updated, subDecksCreated);
                }

                foreach (var subData in data.SubDecks)
                {
                    if (string.IsNullOrWhiteSpace(subData.Name)) continue;

                    var existingSub = targetDeck.SubDecks.FirstOrDefault(sd => 
                        sd.Name.Equals(subData.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingSub == null)
                    {
                        existingSub = new Deck
                        {
                            Name = subData.Name,
                            ParentDeckId = targetDeck.Id
                        };
                        context.Decks.Add(existingSub);
                        await context.SaveChangesAsync();
                        targetDeck.SubDecks.Add(existingSub);
                        subDecksCreated++;
                    }

                    var nestedResult = await ImportJsonDeckAsync(
                        context,
                        subData,
                        existingSub,
                        options,
                        depth + 1,
                        warnings);
                    imported += nestedResult.imported;
                    skipped += nestedResult.skipped;
                    updated += nestedResult.updated;
                    subDecksCreated += nestedResult.subDecksCreated;
                }
            }

            return (imported, skipped, updated, subDecksCreated);
        }

        private async Task<(int imported, int skipped, int updated)> ImportCardAsync(
            FlashcardDbContext context,
            JsonCard cardData,
            int deckId,
            ImportOptions options)
        {
            var front = cardData.Front?.Trim() ?? "";
            var back = cardData.Back?.Trim() ?? "";

            // Duplikat-Prüfung
            var existingCard = await context.Cards.FirstOrDefaultAsync(c =>
                c.DeckId == deckId && c.Front == front);

            if (existingCard != null)
            {
                switch (options.OnDuplicate)
                {
                    case DuplicateHandling.Skip:
                        return (0, 1, 0);

                    case DuplicateHandling.Replace:
                        existingCard.Back = back;
                        await context.SaveChangesAsync();
                        return (0, 0, 1);

                    case DuplicateHandling.KeepBoth:
                        break;
                }
            }

            var card = new Card
            {
                Front = front,
                Back = back,
                DeckId = deckId
            };
            
            context.Cards.Add(card);
            await context.SaveChangesAsync();

            return (1, 0, 0);
        }

        #endregion
    }
}
