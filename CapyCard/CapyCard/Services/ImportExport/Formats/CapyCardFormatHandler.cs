using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.Services.ImportExport.Formats
{
    /// <summary>
    /// Handler für das CapyCard-eigene Format (.capycard).
    /// Das Format ist ein ZIP-Archiv mit einer deck.json Datei.
    /// </summary>
    public class CapyCardFormatHandler : IFormatHandler
    {
        private const string JsonFileName = "deck.json";

        public string[] SupportedExtensions => new[] { ".capycard" };
        public string FormatName => "CapyCard";
        public string FormatDescription => "Das native Format von CapyCard. Enthält alle Karten mit Formatierung, Bildern und optional deinen Lernfortschritt.";
        public bool IsAvailable => true;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <inheritdoc/>
        public async Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName)
        {
            try
            {
                var data = await ReadCapyCardDataAsync(stream);
                if (data == null)
                {
                    return ImportPreview.Failed("Ungültiges CapyCard-Format: Keine deck.json gefunden.");
                }

                var cardCount = CountCards(data.Deck);
                var subDeckCount = CountSubDecks(data.Deck);
                var hasProgress = HasProgressData(data.Deck);

                var preview = ImportPreview.Successful(FormatName, data.Deck.Name, cardCount, subDeckCount);
                preview.HasProgress = hasProgress;
                preview.HasMedia = data.Media?.Count > 0 || HasEmbeddedImages(data.Deck);

                return preview;
            }
            catch (Exception ex)
            {
                return ImportPreview.Failed($"Fehler beim Lesen der Datei: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
            try
            {
                var data = await ReadCapyCardDataAsync(stream);
                if (data == null)
                {
                    return ImportResult.Failed("Ungültiges CapyCard-Format: Keine deck.json gefunden.");
                }

                using var context = new FlashcardDbContext();

                Deck targetDeck;
                int subDecksCreated = 0;

                switch (options.Target)
                {
                    case ImportTarget.NewDeck:
                        // Neues Fach erstellen
                        var deckName = options.NewDeckName ?? data.Deck.Name;
                        targetDeck = new Deck { Name = deckName };
                        context.Decks.Add(targetDeck);
                        await context.SaveChangesAsync();

                        // Standard-Unterdeck erstellen
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

                    case ImportTarget.ExistingSubDeck:
                        if (!options.TargetDeckId.HasValue)
                        {
                            return ImportResult.Failed("Kein Ziel-Thema ausgewählt.");
                        }
                        // Bei SubDeck importieren wir in den Parent
                        var subDeck = await context.Decks.FirstOrDefaultAsync(d => d.Id == options.TargetDeckId.Value);
                        if (subDeck?.ParentDeckId == null)
                        {
                            return ImportResult.Failed("Ziel-Thema nicht gefunden oder ist kein Unterdeck.");
                        }
                        targetDeck = await context.Decks
                            .Include(d => d.SubDecks)
                            .FirstOrDefaultAsync(d => d.Id == subDeck.ParentDeckId);
                        if (targetDeck == null)
                        {
                            return ImportResult.Failed("Übergeordnetes Fach nicht gefunden.");
                        }
                        break;

                    default:
                        return ImportResult.Failed("Ungültiges Import-Ziel.");
                }

                // Karten importieren
                var (cardsImported, cardsSkipped, cardsUpdated, additionalSubDecks) =
                    await ImportDeckDataAsync(context, data.Deck, targetDeck, options);

                subDecksCreated += additionalSubDecks;

                var result = ImportResult.Successful(cardsImported, subDecksCreated, targetDeck.Id);
                result.CardsSkipped = cardsSkipped;
                result.CardsUpdated = cardsUpdated;
                return result;
            }
            catch (Exception ex)
            {
                return ImportResult.Failed($"Fehler beim Import: {ex.Message}");
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

                var exportData = new CapyCardExportData
                {
                    ExportDate = DateTime.UtcNow,
                    Deck = await CreateDeckDataAsync(context, deck, options)
                };

                await WriteCapyCardDataAsync(stream, exportData);

                var cardCount = CountCards(exportData.Deck);
                var subDeckCount = CountSubDecks(exportData.Deck);

                return ExportResult.SuccessfulWithData(Array.Empty<byte>(), cardCount, subDeckCount);
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"Fehler beim Export: {ex.Message}");
            }
        }

        #region Private Helper Methods

        private async Task<CapyCardExportData?> ReadCapyCardDataAsync(Stream stream)
        {
            // Stream in MemoryStream kopieren für wiederholten Zugriff
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var jsonEntry = archive.GetEntry(JsonFileName);
            if (jsonEntry == null)
            {
                return null;
            }

            using var entryStream = jsonEntry.Open();
            return await JsonSerializer.DeserializeAsync<CapyCardExportData>(entryStream, JsonOptions);
        }

        private async Task WriteCapyCardDataAsync(Stream stream, CapyCardExportData data)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            var jsonEntry = archive.CreateEntry(JsonFileName, CompressionLevel.Optimal);

            using var entryStream = jsonEntry.Open();
            await JsonSerializer.SerializeAsync(entryStream, data, JsonOptions);
        }

        private async Task<CapyCardDeckData> CreateDeckDataAsync(
            FlashcardDbContext context,
            Deck deck,
            ExportOptions options)
        {
            var deckData = new CapyCardDeckData
            {
                Name = deck.Name,
                IsDefault = deck.IsDefault
            };

            // Karten hinzufügen
            IEnumerable<Card> cards;

            if (options.Scope == ExportScope.SelectedCards && options.SelectedCardIds?.Count > 0)
            {
                cards = deck.Cards.Where(c => options.SelectedCardIds.Contains(c.Id));
            }
            else
            {
                cards = deck.Cards;
            }

            foreach (var card in cards)
            {
                var cardData = new CapyCardCardData
                {
                    Front = card.Front,
                    Back = card.Back
                };

                if (options.IncludeProgress)
                {
                    var score = await context.CardSmartScores.FirstOrDefaultAsync(s => s.CardId == card.Id);
                    if (score != null)
                    {
                        cardData.Progress = new CapyCardProgressData
                        {
                            Score = score.Score,
                            BoxIndex = score.BoxIndex,
                            LastReviewed = score.LastReviewed
                        };
                    }
                }

                deckData.Cards.Add(cardData);
            }

            // SubDecks hinzufügen (außer bei SelectedCards-Scope)
            if (options.Scope != ExportScope.SelectedCards)
            {
                IEnumerable<Deck> subDecks;

                if (options.Scope == ExportScope.SelectedSubDecks && options.SelectedSubDeckIds?.Count > 0)
                {
                    subDecks = deck.SubDecks.Where(sd => options.SelectedSubDeckIds.Contains(sd.Id));
                }
                else
                {
                    subDecks = deck.SubDecks;
                }

                foreach (var subDeck in subDecks)
                {
                    var subDeckOptions = new ExportOptions
                    {
                        DeckId = subDeck.Id,
                        IncludeProgress = options.IncludeProgress,
                        Scope = ExportScope.FullDeck // SubDecks werden immer vollständig exportiert
                    };
                    deckData.SubDecks.Add(await CreateDeckDataAsync(context, subDeck, subDeckOptions));
                }
            }

            return deckData;
        }

        private async Task<(int imported, int skipped, int updated, int subDecksCreated)> ImportDeckDataAsync(
            FlashcardDbContext context,
            CapyCardDeckData deckData,
            Deck targetDeck,
            ImportOptions options)
        {
            int imported = 0, skipped = 0, updated = 0, subDecksCreated = 0;

            // Finde oder erstelle das Standard-Unterdeck für direkte Karten
            var defaultSubDeck = targetDeck.SubDecks.FirstOrDefault(sd => sd.IsDefault);
            if (defaultSubDeck == null && deckData.Cards.Count > 0)
            {
                defaultSubDeck = new Deck
                {
                    Name = "Allgemein",
                    ParentDeckId = targetDeck.Id,
                    IsDefault = true
                };
                context.Decks.Add(defaultSubDeck);
                await context.SaveChangesAsync();
                subDecksCreated++;
            }

            // Karten im Haupt-Deck importieren (in Standard-Unterdeck)
            if (defaultSubDeck != null)
            {
                foreach (var cardData in deckData.Cards)
                {
                    var result = await ImportCardAsync(context, cardData, defaultSubDeck.Id, options);
                    imported += result.imported;
                    skipped += result.skipped;
                    updated += result.updated;
                }
            }

            // SubDecks importieren
            foreach (var subDeckData in deckData.SubDecks)
            {
                // Prüfen ob SubDeck bereits existiert
                var existingSubDeck = targetDeck.SubDecks.FirstOrDefault(sd =>
                    sd.Name.Equals(subDeckData.Name, StringComparison.OrdinalIgnoreCase));

                if (existingSubDeck == null)
                {
                    existingSubDeck = new Deck
                    {
                        Name = subDeckData.Name,
                        ParentDeckId = targetDeck.Id,
                        IsDefault = subDeckData.IsDefault
                    };
                    context.Decks.Add(existingSubDeck);
                    await context.SaveChangesAsync();
                    subDecksCreated++;
                }

                // Karten im SubDeck importieren
                foreach (var cardData in subDeckData.Cards)
                {
                    var result = await ImportCardAsync(context, cardData, existingSubDeck.Id, options);
                    imported += result.imported;
                    skipped += result.skipped;
                    updated += result.updated;
                }

                // Rekursiv weitere SubDecks verarbeiten (falls vorhanden)
                if (subDeckData.SubDecks.Count > 0)
                {
                    var nestedResult = await ImportDeckDataAsync(context, subDeckData, existingSubDeck, options);
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
            CapyCardCardData cardData,
            int deckId,
            ImportOptions options)
        {
            // Duplikat-Prüfung
            var existingCard = await context.Cards.FirstOrDefaultAsync(c =>
                c.DeckId == deckId &&
                c.Front == cardData.Front);

            if (existingCard != null)
            {
                switch (options.OnDuplicate)
                {
                    case DuplicateHandling.Skip:
                        return (0, 1, 0);

                    case DuplicateHandling.Replace:
                        existingCard.Back = cardData.Back;
                        if (options.IncludeProgress && cardData.Progress != null)
                        {
                            await UpdateCardProgressAsync(context, existingCard.Id, cardData.Progress);
                        }
                        await context.SaveChangesAsync();
                        return (0, 0, 1);

                    case DuplicateHandling.KeepBoth:
                        // Fall through - erstelle neue Karte
                        break;
                }
            }

            // Neue Karte erstellen
            var card = new Card
            {
                Front = cardData.Front,
                Back = cardData.Back,
                DeckId = deckId
            };
            context.Cards.Add(card);
            await context.SaveChangesAsync();

            // Lernfortschritt importieren
            if (options.IncludeProgress && cardData.Progress != null)
            {
                await UpdateCardProgressAsync(context, card.Id, cardData.Progress);
            }

            return (1, 0, 0);
        }

        private async Task UpdateCardProgressAsync(
            FlashcardDbContext context,
            int cardId,
            CapyCardProgressData progressData)
        {
            var existingScore = await context.CardSmartScores.FirstOrDefaultAsync(s => s.CardId == cardId);

            if (existingScore != null)
            {
                existingScore.Score = progressData.Score;
                existingScore.BoxIndex = progressData.BoxIndex;
                existingScore.LastReviewed = progressData.LastReviewed;
            }
            else
            {
                context.CardSmartScores.Add(new CardSmartScore
                {
                    CardId = cardId,
                    Score = progressData.Score,
                    BoxIndex = progressData.BoxIndex,
                    LastReviewed = progressData.LastReviewed
                });
            }

            await context.SaveChangesAsync();
        }

        private static int CountCards(CapyCardDeckData deck)
        {
            return deck.Cards.Count + deck.SubDecks.Sum(CountCards);
        }

        private static int CountSubDecks(CapyCardDeckData deck)
        {
            return deck.SubDecks.Count + deck.SubDecks.Sum(CountSubDecks);
        }

        private static bool HasProgressData(CapyCardDeckData deck)
        {
            return deck.Cards.Any(c => c.Progress != null) ||
                   deck.SubDecks.Any(HasProgressData);
        }

        private static bool HasEmbeddedImages(CapyCardDeckData deck)
        {
            return deck.Cards.Any(c =>
                       c.Front.Contains("![") || c.Back.Contains("![") ||
                       c.Front.Contains("data:image") || c.Back.Contains("data:image")) ||
                   deck.SubDecks.Any(HasEmbeddedImages);
        }

        #endregion
    }
}
