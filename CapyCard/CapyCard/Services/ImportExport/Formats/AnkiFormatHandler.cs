using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using CapyCard.Services.ImportExport.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZstdSharp;

namespace CapyCard.Services.ImportExport.Formats
{
    /// <summary>
    /// Handler für Anki .apkg Format (Import und Export).
    /// Unterstützt Anki 2.1+ (Schema Version 11+).
    /// </summary>
    public class AnkiFormatHandler : IFormatHandler
    {
        public string[] SupportedExtensions => new[] { ".apkg" };
        public string FormatName => "Anki";
        public string FormatDescription => "Kompatibel mit Anki und AnkiDroid. Ermöglicht den Austausch mit Millionen von geteilten Kartenstapeln auf ankiweb.net.";

        // Anki ist im Browser nicht verfügbar (SQLite benötigt)
        public bool IsAvailable =>
#if BROWSER
            false;
#else
            true;
#endif

        // Regex für HTML-Bilder: <img src="...">
        private static readonly Regex HtmlImageRegex = new(@"<img[^>]+src=(?:""([^""]*)""|'([^']*)'|([^""'>\s]+))[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Regex für Markdown-Bilder: ![alt](data:...)
        private static readonly Regex MarkdownImageRegex = new(@"!\[([^\]]*)\]\(data:([^;]+);base64,([^)]+)\)", RegexOptions.Compiled);
        // Regex für Anki Sound: [sound:filename.mp3]
        private static readonly Regex SoundRegex = new(@"\[sound:([^\]]+)\]", RegexOptions.Compiled);

        /// <inheritdoc/>
        public async Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName)
        {
#if BROWSER
            return ImportPreview.Failed("Anki-Import ist im Browser nicht verfügbar.");
#else
            try
            {
                using var tempDir = new TempDirectory();
                var apkgPath = Path.Combine(tempDir.Path, "deck.apkg");
                await using (var fileStream = File.Create(apkgPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                ZipFile.ExtractToDirectory(apkgPath, tempDir.Path);

                var dbPath = GetBestDatabasePath(tempDir.Path);

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    return ImportPreview.Failed("Ungültiges Anki-Paket: Keine gültige Datenbank gefunden.");
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await connection.OpenAsync();

                var deckName = await GetDeckNameAsync(connection);
                var cardCount = await GetCardCountAsync(connection);
                var subDeckCount = await GetSubDeckCountAsync(connection);
                var hasProgress = await HasProgressDataAsync(connection);

                var mediaPath = Path.Combine(tempDir.Path, "media");
                var hasMedia = File.Exists(mediaPath) && new FileInfo(mediaPath).Length > 2;

                var preview = ImportPreview.Successful(FormatName, deckName, cardCount, subDeckCount);
                preview.HasProgress = hasProgress;
                preview.HasMedia = hasMedia;

                return preview;
            }
            catch (Exception ex)
            {
                return ImportPreview.Failed($"Fehler beim Analysieren der Anki-Datei: {ex.Message}");
            }
#endif
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
#if BROWSER
            return ImportResult.Failed("Anki-Import ist im Browser nicht verfügbar.");
#else
            try
            {
                using var tempDir = new TempDirectory();
                var apkgPath = Path.Combine(tempDir.Path, "deck.apkg");
                await using (var fileStream = File.Create(apkgPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                ZipFile.ExtractToDirectory(apkgPath, tempDir.Path);

                var dbPath = GetBestDatabasePath(tempDir.Path);

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    return ImportResult.Failed("Ungültiges Anki-Paket.");
                }

                var mediaMap = await LoadMediaMapAsync(tempDir.Path);

                using var ankiConnection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await ankiConnection.OpenAsync();

                using var context = new FlashcardDbContext();

                Deck targetDeck;
                int subDecksCreated = 0;

                var deckName = options.NewDeckName ?? await GetDeckNameAsync(ankiConnection);

                switch (options.Target)
                {
                    case ImportTarget.NewDeck:
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
                        return ImportResult.Failed("Anki-Import unterstützt nur 'Neues Fach' oder 'In bestehendes Fach'.");
                }

                var ankiDecks = await LoadAnkiDecksAsync(ankiConnection);
                var subDeckCache = targetDeck.SubDecks.ToDictionary(sd => sd.Name.ToLowerInvariant(), sd => sd);

                int imported = 0, skipped = 0, updated = 0;
                var warnings = new List<string>();

                var notesQuery = @"
                    SELECT n.id, n.flds, n.mid, c.did, c.ivl, c.factor, c.queue, c.type
                    FROM notes n
                    JOIN cards c ON c.nid = n.id
                    WHERE n.flds IS NOT NULL";

                using var cmd = new SqliteCommand(notesQuery, ankiConnection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var fields = reader.GetString(1).Split('\x1f');
                    if (fields.Length < 2)
                    {
                        warnings.Add("Karte mit weniger als 2 Feldern übersprungen.");
                        continue;
                    }

                    var front = ConvertAnkiToMarkdown(fields[0], mediaMap, tempDir.Path);
                    var back = ConvertAnkiToMarkdown(fields[1], mediaMap, tempDir.Path);

                    var ankiDeckId = reader.GetInt64(3);
                    var subDeckName = GetSubDeckName(ankiDecks, ankiDeckId);

                    Deck cardDeck;
                    var subDeckKey = subDeckName.ToLowerInvariant();

                    if (!subDeckCache.TryGetValue(subDeckKey, out cardDeck!))
                    {
                        cardDeck = new Deck
                        {
                            Name = subDeckName,
                            ParentDeckId = targetDeck.Id,
                            IsDefault = subDeckName == "Allgemein"
                        };
                        context.Decks.Add(cardDeck);
                        await context.SaveChangesAsync();
                        subDeckCache[subDeckKey] = cardDeck;
                        subDecksCreated++;
                    }

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
                                continue;
                            case DuplicateHandling.KeepBoth:
                                break;
                        }
                    }

                    var card = new Card
                    {
                        Front = front,
                        Back = back,
                        DeckId = cardDeck.Id
                    };
                    context.Cards.Add(card);
                    await context.SaveChangesAsync();

                    if (options.IncludeProgress && !reader.IsDBNull(4))
                    {
                        var interval = reader.GetInt32(4);
                        var boxIndex = IntervalToBoxIndex(interval);

                        var score = new CardSmartScore
                        {
                            CardId = card.Id,
                            BoxIndex = boxIndex,
                            Score = boxIndex * 0.2,
                            LastReviewed = DateTime.UtcNow.AddDays(-interval)
                        };
                        context.CardSmartScores.Add(score);
                    }

                    imported++;
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
                return ImportResult.Failed($"Fehler beim Anki-Import: {ex.Message}");
            }
#endif
        }

        /// <inheritdoc/>
        public async Task<ExportResult> ExportAsync(Stream stream, ExportOptions options)
        {
#if BROWSER
            return ExportResult.Failed("Anki-Export ist im Browser nicht verfügbar.");
#else
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

                using var tempDir = new TempDirectory();
                var dbPath = Path.Combine(tempDir.Path, "collection.anki2");

                await CreateAnkiDatabaseAsync(dbPath);

                var mediaDict = new Dictionary<string, string>();
                int mediaCounter = 0;

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync();

                var ankiDecks = new Dictionary<string, long>();
                long deckIdCounter = 1;
                ankiDecks[deck.Name] = deckIdCounter++;

                IEnumerable<Deck> subDecksToExport;
                if (options.Scope == ExportScope.SelectedSubDecks && options.SelectedSubDeckIds?.Count > 0)
                {
                    subDecksToExport = deck.SubDecks.Where(sd => options.SelectedSubDeckIds.Contains(sd.Id));
                }
                else if (options.Scope != ExportScope.SelectedCards)
                {
                    subDecksToExport = deck.SubDecks;
                }
                else
                {
                    subDecksToExport = Enumerable.Empty<Deck>();
                }

                foreach (var subDeck in subDecksToExport)
                {
                    var ankiDeckName = $"{deck.Name}::{subDeck.Name}";
                    ankiDecks[ankiDeckName] = deckIdCounter++;
                }

                var decksJson = CreateDecksJson(ankiDecks);
                var modelsJson = CreateBasicModelJson();

                await InsertColAsync(connection, decksJson, modelsJson);

                long noteId = 1, cardId = 1;
                int cardCount = 0;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                async Task ExportCard(Card card, long ankiDeckId)
                {
                    var front = ConvertMarkdownToAnki(card.Front, tempDir.Path, mediaDict, ref mediaCounter);
                    var back = ConvertMarkdownToAnki(card.Back, tempDir.Path, mediaDict, ref mediaCounter);
                    var flds = $"{front}\x1f{back}";

                    int interval = 0, factor = 2500, queue = 0, type = 0;
                    if (options.IncludeProgress)
                    {
                        var score = await context.CardSmartScores.FirstOrDefaultAsync(s => s.CardId == card.Id);
                        if (score != null)
                        {
                            interval = BoxIndexToInterval(score.BoxIndex);
                            factor = 2500 + (score.BoxIndex * 100);
                            queue = score.BoxIndex > 0 ? 2 : 0;
                            type = score.BoxIndex > 0 ? 2 : 0;
                        }
                    }

                    await InsertNoteAsync(connection, noteId, 1, flds, now);
                    await InsertCardAsync(connection, cardId, noteId, ankiDeckId, interval, factor, queue, type, now);

                    noteId++;
                    cardId++;
                    cardCount++;
                }

                if (options.Scope == ExportScope.SelectedCards && options.SelectedCardIds?.Count > 0)
                {
                    var selectedCards = await context.Cards
                        .Where(c => options.SelectedCardIds.Contains(c.Id))
                        .ToListAsync();

                    foreach (var card in selectedCards)
                    {
                        await ExportCard(card, ankiDecks[deck.Name]);
                    }
                }
                else
                {
                    foreach (var subDeck in subDecksToExport)
                    {
                        var ankiDeckName = $"{deck.Name}::{subDeck.Name}";
                        var ankiDeckId = ankiDecks[ankiDeckName];

                        foreach (var card in subDeck.Cards)
                        {
                            await ExportCard(card, ankiDeckId);
                        }
                    }
                }

                connection.Close();

                var mediaJson = JsonSerializer.Serialize(mediaDict.ToDictionary(kvp => kvp.Value, kvp => kvp.Key));
                await File.WriteAllTextAsync(Path.Combine(tempDir.Path, "media"), mediaJson);

                var apkgPath = Path.Combine(tempDir.Path, "export.apkg");
                using (var archive = ZipFile.Open(apkgPath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(dbPath, "collection.anki2");
                    archive.CreateEntryFromFile(Path.Combine(tempDir.Path, "media"), "media");

                    foreach (var kvp in mediaDict)
                    {
                        var mediaFilePath = Path.Combine(tempDir.Path, kvp.Value);
                        if (File.Exists(mediaFilePath))
                        {
                            archive.CreateEntryFromFile(mediaFilePath, kvp.Value);
                        }
                    }
                }

                await using var apkgStream = File.OpenRead(apkgPath);
                await apkgStream.CopyToAsync(stream);

                return ExportResult.SuccessfulWithData(Array.Empty<byte>(), cardCount, subDecksToExport.Count());
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"Fehler beim Anki-Export: {ex.Message}");
            }
#endif
        }

        #region Private Helper Methods

#if !BROWSER
        private static async Task<string> GetDeckNameAsync(SqliteConnection connection)
        {
            var decks = await LoadAnkiDecksAsync(connection);
            if (decks.Count == 0) return "Importiertes Deck";

            var userDecks = decks.Where(d => d.Key != 1 && d.Value != "Default").ToList();
            if (userDecks.Count == 0) return "Importiertes Deck";
            
            var names = userDecks.Select(d => d.Value).ToList();
            var commonPrefix = GetCommonPrefix(names);

            if (!string.IsNullOrEmpty(commonPrefix))
            {
                var cleanPrefix = commonPrefix.Split(new[] { "::", " ☰ ", "\x1f" }, StringSplitOptions.RemoveEmptyEntries).Last();
                return cleanPrefix;
            }

            return userDecks[0].Value.Split(new[] { "::", " ☰ ", "\x1f" }, StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private static string GetCommonPrefix(List<string> strings)
        {
            if (strings.Count == 0) return "";
            var prefix = strings[0];
            foreach (var s in strings.Skip(1))
            {
                while (!s.StartsWith(prefix))
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                    if (string.IsNullOrEmpty(prefix)) return "";
                }
            }
            if (prefix.EndsWith(":") || prefix.EndsWith(" ") || prefix.EndsWith("\x1f")) prefix = prefix.TrimEnd(':', ' ', '\x1f');
            return prefix;
        }

        private static async Task<int> GetCardCountAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM cards", connection);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private static async Task<int> GetSubDeckCountAsync(SqliteConnection connection)
        {
            var decks = await LoadAnkiDecksAsync(connection);
            return decks.Count(d => d.Key != 1 && d.Value != "Default");
        }

        private static async Task<bool> HasProgressDataAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM cards WHERE ivl > 0", connection);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        private static async Task<Dictionary<string, string>> LoadMediaMapAsync(string tempDirPath)
        {
            var mediaPath = Path.Combine(tempDirPath, "media");
            if (!File.Exists(mediaPath))
                return new Dictionary<string, string>();

            var json = await File.ReadAllTextAsync(mediaPath);
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private static async Task<Dictionary<long, string>> LoadAnkiDecksAsync(SqliteConnection connection)
        {
            var decks = new Dictionary<long, string>();
            try
            {
                using var cmdNew = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='decks'", connection);
                var tableExists = await cmdNew.ExecuteScalarAsync();
                
                if (tableExists != null)
                {
                    using var cmdDecks = new SqliteCommand("SELECT id, name FROM decks", connection);
                    using var reader = await cmdDecks.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        decks[reader.GetInt64(0)] = reader.GetString(1);
                    }
                }

                if (decks.Count == 0)
                {
                    using var cmd = new SqliteCommand("SELECT decks FROM col LIMIT 1", connection);
                    var decksJson = await cmd.ExecuteScalarAsync() as string;
                    if (!string.IsNullOrEmpty(decksJson))
                    {
                        using var doc = JsonDocument.Parse(decksJson);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                if (long.TryParse(prop.Name, out var deckId))
                                {
                                    var nameProp = prop.Value.EnumerateObject()
                                        .FirstOrDefault(p => p.Name.Equals("name", StringComparison.OrdinalIgnoreCase));
                                    decks[deckId] = nameProp.Value.ValueKind == JsonValueKind.String ? nameProp.Value.GetString() ?? "Default" : $"Deck {deckId}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Anki-Decks: {ex.Message}");
            }
            return decks;
        }

        private static string GetSubDeckName(Dictionary<long, string> ankiDecks, long deckId)
        {
            if (ankiDecks.TryGetValue(deckId, out var deckName))
            {
                var parts = deckName.Split(new[] { "::", " ☰ ", "\x1f" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var lastName = parts.Last();
                    return lastName == "Default" ? "Allgemein" : lastName;
                }
            }
            return $"Deck {deckId}";
        }

        private static string ConvertAnkiToMarkdown(string html, Dictionary<string, string> mediaMap, string tempDirPath)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            var result = html;

            // HTML-Quelltext-Umbrüche normalisieren
            result = result.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

            // Bilder
            result = HtmlImageRegex.Replace(result, match =>
            {
                var originalSrc = match.Groups[1].Success ? match.Groups[1].Value :
                                  match.Groups[2].Success ? match.Groups[2].Value :
                                  match.Groups[3].Value;

                var src = System.Net.WebUtility.UrlDecode(originalSrc);
                var srcNfc = src.Normalize(NormalizationForm.FormC);
                var srcNfd = src.Normalize(NormalizationForm.FormD);

                string? mediaKey = null;
                foreach (var entry in mediaMap)
                {
                    var val = entry.Value;
                    var valDecoded = System.Net.WebUtility.UrlDecode(val);
                    if (val == src || val == originalSrc || valDecoded == src || 
                        val.Normalize(NormalizationForm.FormC) == srcNfc ||
                        val.Normalize(NormalizationForm.FormD) == srcNfd)
                    {
                        mediaKey = entry.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(mediaKey))
                {
                    var mediaFilePath = Path.Combine(tempDirPath, mediaKey);
                    if (File.Exists(mediaFilePath))
                    {
                        var bytes = File.ReadAllBytes(mediaFilePath);
                        var base64 = Convert.ToBase64String(bytes);
                        var mimeType = GetMimeType(mediaMap[mediaKey] ?? src);
                        return $"![Bild](data:{mimeType};base64,{base64})";
                    }
                }
                return $"[Bild: {src}]";
            });

            // Sound
            result = SoundRegex.Replace(result, "");

            // Blöcke zu Newlines
            result = result.Replace("<div>", "\n").Replace("</div>", "")
                           .Replace("<p>", "\n").Replace("</p>", "\n")
                           .Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");

            // Listen
            result = result.Replace("<ul>", "\n").Replace("</ul>", "\n")
                           .Replace("<ol>", "\n").Replace("</ol>", "\n")
                           .Replace("<li>", "\n- ").Replace("</li>", "");

            // Formatierung
            result = result.Replace("<b>", "**").Replace("</b>", "**")
                           .Replace("<strong>", "**").Replace("</strong>", "**")
                           .Replace("<i>", "_").Replace("</i>", "_")
                           .Replace("<em>", "_").Replace("</em>", "_");

            // Entitäten
            result = System.Net.WebUtility.HtmlDecode(result);

            // Tags entfernen
            result = Regex.Replace(result, @"<[^>]+>", "");

            // Whitespace (Doppelte Leerzeichen reduzieren)
            result = Regex.Replace(result, @"[ \t]+", " ");

            // Mehrfache Newlines reduzieren
            result = Regex.Replace(result, @"\n{3,}", "\n\n");
            
            return result.Trim();
        }

        private static string ConvertMarkdownToAnki(string markdown, string tempDirPath, Dictionary<string, string> mediaDict, ref int mediaCounter)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var result = markdown;
            var counter = mediaCounter;

            result = MarkdownImageRegex.Replace(result, match =>
            {
                var mimeType = match.Groups[2].Value;
                var base64 = match.Groups[3].Value;
                var extension = mimeType.Split('/')[1];
                var fileName = $"img_{counter}.{extension}";
                File.WriteAllBytes(Path.Combine(tempDirPath, counter.ToString()), Convert.FromBase64String(base64));
                mediaDict[fileName] = counter.ToString();
                return $"<img src=\"{counter++}\">";
            });

            mediaCounter = counter;
            result = result.Replace("\n", "<br>");
            result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "<b>$1</b>");
            result = Regex.Replace(result, @"_([^_]+)_", "<i>$1</i>");
            return result;
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        private static int IntervalToBoxIndex(int interval)
        {
            return interval switch { <= 0 => 0, <= 1 => 1, <= 3 => 2, <= 7 => 3, <= 21 => 4, _ => 5 };
        }

        private static int BoxIndexToInterval(int boxIndex)
        {
            return boxIndex switch { 0 => 0, 1 => 1, 2 => 3, 3 => 7, 4 => 21, _ => 60 };
        }

        private static async Task CreateAnkiDatabaseAsync(string dbPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();
            var schema = @"
                CREATE TABLE col (id INTEGER PRIMARY KEY, crt INTEGER NOT NULL, mod INTEGER NOT NULL, scm INTEGER NOT NULL, ver INTEGER NOT NULL, dty INTEGER NOT NULL, usn INTEGER NOT NULL, ls INTEGER NOT NULL, conf TEXT NOT NULL, models TEXT NOT NULL, decks TEXT NOT NULL, dconf TEXT NOT NULL, tags TEXT NOT NULL);
                CREATE TABLE notes (id INTEGER PRIMARY KEY, guid TEXT NOT NULL, mid INTEGER NOT NULL, mod INTEGER NOT NULL, usn INTEGER NOT NULL, tags TEXT NOT NULL, flds TEXT NOT NULL, sfld TEXT NOT NULL, csum INTEGER NOT NULL, flags INTEGER NOT NULL, data TEXT NOT NULL);
                CREATE TABLE cards (id INTEGER PRIMARY KEY, nid INTEGER NOT NULL, did INTEGER NOT NULL, ord INTEGER NOT NULL, mod INTEGER NOT NULL, usn INTEGER NOT NULL, type INTEGER NOT NULL, queue INTEGER NOT NULL, due INTEGER NOT NULL, ivl INTEGER NOT NULL, factor INTEGER NOT NULL, reps INTEGER NOT NULL, lapses INTEGER NOT NULL, left INTEGER NOT NULL, odue INTEGER NOT NULL, odid INTEGER NOT NULL, flags INTEGER NOT NULL, data TEXT NOT NULL);
                CREATE TABLE revlog (id INTEGER PRIMARY KEY, cid INTEGER NOT NULL, usn INTEGER NOT NULL, ease INTEGER NOT NULL, ivl INTEGER NOT NULL, lastIvl INTEGER NOT NULL, factor INTEGER NOT NULL, time INTEGER NOT NULL, type INTEGER NOT NULL);
                CREATE TABLE graves (usn INTEGER NOT NULL, oid INTEGER NOT NULL, type INTEGER NOT NULL);
                CREATE INDEX ix_cards_nid ON cards (nid);
                CREATE INDEX ix_cards_sched ON cards (did, queue, due);
                CREATE INDEX ix_notes_csum ON notes (csum);
                CREATE INDEX ix_notes_usn ON notes (usn);
                CREATE INDEX ix_cards_usn ON cards (usn);
                CREATE INDEX ix_revlog_cid ON revlog (cid);
                CREATE INDEX ix_revlog_usn ON revlog (usn);";
            using var cmd = new SqliteCommand(schema, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string CreateDecksJson(Dictionary<string, long> decks)
        {
            var deckObjects = new Dictionary<string, object>();
            foreach (var kvp in decks)
            {
                deckObjects[kvp.Value.ToString()] = new { id = kvp.Value, name = kvp.Key, mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), usn = -1, lrnToday = new[] { 0, 0 }, revToday = new[] { 0, 0 }, newToday = new[] { 0, 0 }, timeToday = new[] { 0, 0 }, collapsed = false, desc = "", dyn = 0, conf = 1, extendNew = 10, extendRev = 50 };
            }
            deckObjects["1"] = new { id = 1, name = "Default", mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), usn = -1, lrnToday = new[] { 0, 0 }, revToday = new[] { 0, 0 }, newToday = new[] { 0, 0 }, timeToday = new[] { 0, 0 }, collapsed = false, desc = "", dyn = 0, conf = 1, extendNew = 10, extendRev = 50 };
            return JsonSerializer.Serialize(deckObjects);
        }

        private static string CreateBasicModelJson()
        {
            var models = new Dictionary<string, object> { ["1"] = new { id = 1, name = "Basic", type = 0, mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), usn = -1, sortf = 0, did = 1, tmpls = new[] { new { name = "Card 1", ord = 0, qfmt = "{{Front}}", afmt = "{{FrontSide}}<hr id=answer>{{Back}}", did = (long?)null, bqfmt = "", bafmt = "" } }, flds = new[] { new { name = "Front", ord = 0, sticky = false, rtl = false, font = "Arial", size = 20, media = Array.Empty<string>() }, new { name = "Back", ord = 1, sticky = false, rtl = false, font = "Arial", size = 20, media = Array.Empty<string>() } }, css = ".card { font-family: arial; font-size: 20px; text-align: center; color: black; background-color: white; }", latexPre = "", latexPost = "", latexsvg = false, req = new[] { new object[] { 0, "all", new[] { 0 } } }, tags = Array.Empty<string>(), vers = Array.Empty<string>() } };
            return JsonSerializer.Serialize(models);
        }

        private static async Task InsertColAsync(SqliteConnection connection, string decksJson, string modelsJson)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sql = @"INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags) VALUES (1, @crt, @mod, @scm, 11, 0, -1, 0, @conf, @models, @decks, @dconf, '{}')";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@crt", now); cmd.Parameters.AddWithValue("@mod", now); cmd.Parameters.AddWithValue("@scm", now);
            cmd.Parameters.AddWithValue("@conf", "{}"); cmd.Parameters.AddWithValue("@models", modelsJson); cmd.Parameters.AddWithValue("@decks", decksJson); cmd.Parameters.AddWithValue("@dconf", "{}");
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertNoteAsync(SqliteConnection connection, long id, long mid, string flds, long mod)
        {
            var sfld = flds.Split('\x1f')[0];
            var sql = @"INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data) VALUES (@id, @guid, @mid, @mod, -1, '', @flds, @sfld, 0, 0, '')";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@guid", Guid.NewGuid().ToString()); cmd.Parameters.AddWithValue("@mid", mid); cmd.Parameters.AddWithValue("@mod", mod); cmd.Parameters.AddWithValue("@flds", flds); cmd.Parameters.AddWithValue("@sfld", sfld);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertCardAsync(SqliteConnection connection, long id, long nid, long did, int ivl, int factor, int queue, int type, long mod)
        {
            var sql = @"INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data) VALUES (@id, @nid, @did, 0, @mod, -1, @type, @queue, 0, @ivl, @factor, 0, 0, 0, 0, 0, 0, '')";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@nid", nid); cmd.Parameters.AddWithValue("@did", did); cmd.Parameters.AddWithValue("@mod", mod); cmd.Parameters.AddWithValue("@type", type); cmd.Parameters.AddWithValue("@queue", queue); cmd.Parameters.AddWithValue("@ivl", ivl); cmd.Parameters.AddWithValue("@factor", factor);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string? GetBestDatabasePath(string tempDirPath)
        {
            var zstdPath = Path.Combine(tempDirPath, "collection.anki21b");
            if (File.Exists(zstdPath)) { var decomp = Path.Combine(tempDirPath, "collection.anki2_decomp"); DecompressZstd(zstdPath, decomp); return decomp; }
            var anki21 = Path.Combine(tempDirPath, "collection.anki21"); if (File.Exists(anki21)) return anki21;
            var anki2 = Path.Combine(tempDirPath, "collection.anki2"); if (File.Exists(anki2)) return anki2;
            return null;
        }

        private static void DecompressZstd(string src, string dst)
        {
            using var s = File.OpenRead(src); using var d = File.Create(dst); using var dec = new DecompressionStream(s); dec.CopyTo(d);
        }
#endif
        #endregion

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }
            public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"capycard_import_{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
            public void Dispose() { try { if (Directory.Exists(Path)) Directory.Delete(Path, true); } catch { } }
        }
    }
}
