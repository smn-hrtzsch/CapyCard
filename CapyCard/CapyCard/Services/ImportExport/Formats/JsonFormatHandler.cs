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
        public string FormatDescription => "Importiere Karten direkt aus KI-generiertem Text. Unterstützt verschachtelte Themen und Bilder via Base64.";
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

                var (imported, skipped, updated, additionalSubDecks) = await ImportJsonDeckAsync(context, data, targetDeck, options);
                subDecksCreated += additionalSubDecks;

                var result = ImportResult.Successful(imported, subDecksCreated, targetDeck.Id);
                result.CardsSkipped = skipped;
                result.CardsUpdated = updated;
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
            int count = deck.Cards?.Count ?? 0;
            if (deck.SubDecks != null)
            {
                count += deck.SubDecks.Sum(CountCards);
            }
            return count;
        }

        private int CountSubDecks(JsonDeck deck)
        {
            int count = deck.SubDecks?.Count ?? 0;
            if (deck.SubDecks != null)
            {
                count += deck.SubDecks.Sum(CountSubDecks);
            }
            return count;
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

        private async Task<(int imported, int skipped, int updated, int subDecksCreated)> ImportJsonDeckAsync(
            FlashcardDbContext context,
            JsonDeck data,
            Deck targetDeck,
            ImportOptions options)
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
            if (data.SubDecks != null)
            {
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

                    var nestedResult = await ImportJsonDeckAsync(context, subData, existingSub, options);
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
