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
        private static readonly Regex HtmlImageRegex = new(@"<img\s+src=[""']([^""']+)[""'][^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
                // Stream in temp-Datei extrahieren (ZipArchive braucht seekable stream)
                using var tempDir = new TempDirectory();
                var apkgPath = Path.Combine(tempDir.Path, "deck.apkg");
                await using (var fileStream = File.Create(apkgPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // ZIP entpacken
                ZipFile.ExtractToDirectory(apkgPath, tempDir.Path);

                var dbPath = Path.Combine(tempDir.Path, "collection.anki2");
                if (!File.Exists(dbPath))
                {
                    // Versuche collection.anki21 (neuere Version)
                    dbPath = Path.Combine(tempDir.Path, "collection.anki21");
                }

                if (!File.Exists(dbPath))
                {
                    return ImportPreview.Failed("Ungültiges Anki-Paket: Keine collection.anki2 gefunden.");
                }

                // Datenbank lesen
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await connection.OpenAsync();

                // Deck-Namen ermitteln
                var deckName = await GetDeckNameAsync(connection);

                // Karten zählen
                var cardCount = await GetCardCountAsync(connection);

                // SubDecks zählen
                var subDeckCount = await GetSubDeckCountAsync(connection);

                // Prüfen ob Lernfortschritt vorhanden
                var hasProgress = await HasProgressDataAsync(connection);

                // Prüfen ob Media vorhanden
                var mediaPath = Path.Combine(tempDir.Path, "media");
                var hasMedia = File.Exists(mediaPath) && new FileInfo(mediaPath).Length > 2; // > "{}"

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

                var dbPath = Path.Combine(tempDir.Path, "collection.anki2");
                if (!File.Exists(dbPath))
                {
                    dbPath = Path.Combine(tempDir.Path, "collection.anki21");
                }

                if (!File.Exists(dbPath))
                {
                    return ImportResult.Failed("Ungültiges Anki-Paket.");
                }

                // Media laden
                var mediaMap = await LoadMediaMapAsync(tempDir.Path);

                using var ankiConnection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await ankiConnection.OpenAsync();

                using var context = new FlashcardDbContext();

                // Ziel-Deck erstellen oder ermitteln
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
                        targetDeck = await context.Decks
                            .Include(d => d.SubDecks)
                            .FirstOrDefaultAsync(d => d.Id == options.TargetDeckId.Value);
                        if (targetDeck == null)
                        {
                            return ImportResult.Failed("Ziel-Fach nicht gefunden.");
                        }
                        break;

                    default:
                        return ImportResult.Failed("Anki-Import unterstützt nur 'Neues Fach' oder 'In bestehendes Fach'.");
                }

                // Anki-Decks laden (für SubDeck-Mapping)
                var ankiDecks = await LoadAnkiDecksAsync(ankiConnection);

                // SubDeck-Cache
                var subDeckCache = targetDeck.SubDecks.ToDictionary(sd => sd.Name.ToLowerInvariant(), sd => sd);

                // Notes und Cards laden
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
                    var fields = reader.GetString(1).Split('\x1f'); // Anki field separator
                    if (fields.Length < 2)
                    {
                        warnings.Add("Karte mit weniger als 2 Feldern übersprungen.");
                        continue;
                    }

                    var front = ConvertAnkiToMarkdown(fields[0], mediaMap, tempDir.Path);
                    var back = ConvertAnkiToMarkdown(fields[1], mediaMap, tempDir.Path);

                    // SubDeck ermitteln
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

                    // Lernfortschritt importieren
                    if (options.IncludeProgress && !reader.IsDBNull(4))
                    {
                        var interval = reader.GetInt32(4);
                        var factor = reader.IsDBNull(5) ? 2500 : reader.GetInt32(5);
                        var queue = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);

                        // Interval → BoxIndex approximieren
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

                // SQLite-Datenbank erstellen
                await CreateAnkiDatabaseAsync(dbPath);

                // Media-Verzeichnis
                var mediaDict = new Dictionary<string, string>();
                int mediaCounter = 0;

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync();

                // Anki-Decks erstellen
                var ankiDecks = new Dictionary<string, long>();
                long deckIdCounter = 1;

                // Haupt-Deck
                ankiDecks[deck.Name] = deckIdCounter++;

                // SubDecks mit :: Notation
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

                // Decks-JSON für col-Tabelle erstellen
                var decksJson = CreateDecksJson(ankiDecks);
                var modelsJson = CreateBasicModelJson();

                // col-Tabelle befüllen
                await InsertColAsync(connection, decksJson, modelsJson);

                // Karten exportieren
                long noteId = 1, cardId = 1;
                int cardCount = 0;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                async Task ExportCard(Card card, long ankiDeckId)
                {
                    var front = ConvertMarkdownToAnki(card.Front, tempDir.Path, mediaDict, ref mediaCounter);
                    var back = ConvertMarkdownToAnki(card.Back, tempDir.Path, mediaDict, ref mediaCounter);
                    var flds = $"{front}\x1f{back}";

                    // Lernfortschritt
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

                    // Note einfügen
                    await InsertNoteAsync(connection, noteId, 1, flds, now);

                    // Card einfügen
                    await InsertCardAsync(connection, cardId, noteId, ankiDeckId, interval, factor, queue, type, now);

                    noteId++;
                    cardId++;
                    cardCount++;
                }

                // Karten sammeln und exportieren
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

                // Media-JSON erstellen
                var mediaJson = JsonSerializer.Serialize(mediaDict.ToDictionary(
                    kvp => kvp.Value, // "0", "1", etc.
                    kvp => kvp.Key    // Original filename
                ));
                await File.WriteAllTextAsync(Path.Combine(tempDir.Path, "media"), mediaJson);

                // ZIP erstellen
                var apkgPath = Path.Combine(tempDir.Path, "export.apkg");
                using (var archive = ZipFile.Open(apkgPath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(dbPath, "collection.anki2");
                    archive.CreateEntryFromFile(Path.Combine(tempDir.Path, "media"), "media");

                    // Media-Dateien hinzufügen
                    foreach (var kvp in mediaDict)
                    {
                        var mediaFilePath = Path.Combine(tempDir.Path, kvp.Value);
                        if (File.Exists(mediaFilePath))
                        {
                            archive.CreateEntryFromFile(mediaFilePath, kvp.Value);
                        }
                    }
                }

                // In Output-Stream kopieren
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
            using var cmd = new SqliteCommand("SELECT decks FROM col LIMIT 1", connection);
            var decksJson = await cmd.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(decksJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(decksJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrEmpty(name) && name != "Default")
                            {
                                // Entferne :: Hierarchie für Root-Name
                                return name.Split("::")[0];
                            }
                        }
                    }
                }
                catch { }
            }

            return "Importiertes Deck";
        }

        private static async Task<int> GetCardCountAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM cards", connection);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private static async Task<int> GetSubDeckCountAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT decks FROM col LIMIT 1", connection);
            var decksJson = await cmd.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(decksJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(decksJson);
                    return doc.RootElement.EnumerateObject().Count() - 1; // -1 für Default
                }
                catch { }
            }

            return 0;
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

            using var cmd = new SqliteCommand("SELECT decks FROM col LIMIT 1", connection);
            var decksJson = await cmd.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(decksJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(decksJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (long.TryParse(prop.Name, out var deckId) &&
                            prop.Value.TryGetProperty("name", out var nameProp))
                        {
                            decks[deckId] = nameProp.GetString() ?? "Default";
                        }
                    }
                }
                catch { }
            }

            return decks;
        }

        private static string GetSubDeckName(Dictionary<long, string> ankiDecks, long deckId)
        {
            if (ankiDecks.TryGetValue(deckId, out var deckName))
            {
                // Extrahiere letzten Teil nach ::
                var parts = deckName.Split("::");
                return parts.Length > 1 ? parts[^1] : (parts[0] == "Default" ? "Allgemein" : parts[0]);
            }
            return "Allgemein";
        }

        private static string ConvertAnkiToMarkdown(string html, Dictionary<string, string> mediaMap, string tempDirPath)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            var result = html;

            // HTML-Bilder → Markdown mit Base64
            result = HtmlImageRegex.Replace(result, match =>
            {
                var src = match.Groups[1].Value;

                // Versuche Media-Datei zu laden
                if (mediaMap.TryGetValue(src, out var mediaFileName) || mediaMap.Values.Contains(src))
                {
                    var mediaKey = mediaMap.FirstOrDefault(kvp => kvp.Value == src || kvp.Key == src).Key;
                    if (!string.IsNullOrEmpty(mediaKey))
                    {
                        var mediaFilePath = Path.Combine(tempDirPath, mediaKey);
                        if (File.Exists(mediaFilePath))
                        {
                            var bytes = File.ReadAllBytes(mediaFilePath);
                            var base64 = Convert.ToBase64String(bytes);
                            var mimeType = GetMimeType(mediaFileName ?? src);
                            return $"![Bild](data:{mimeType};base64,{base64})";
                        }
                    }
                }

                return $"[Bild: {src}]";
            });

            // Sound → Platzhalter
            result = SoundRegex.Replace(result, match => $"[Audio: {match.Groups[1].Value}]");

            // Einfache HTML → Markdown Konvertierung
            result = result
                .Replace("<br>", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br />", "\n")
                .Replace("<b>", "**")
                .Replace("</b>", "**")
                .Replace("<strong>", "**")
                .Replace("</strong>", "**")
                .Replace("<i>", "_")
                .Replace("</i>", "_")
                .Replace("<em>", "_")
                .Replace("</em>", "_")
                .Replace("<u>", "")
                .Replace("</u>", "")
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");

            // Restliche HTML-Tags entfernen
            result = Regex.Replace(result, @"<[^>]+>", "");

            return result.Trim();
        }

        private static string ConvertMarkdownToAnki(string markdown, string tempDirPath, Dictionary<string, string> mediaDict, ref int mediaCounter)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var result = markdown;
            var counter = mediaCounter;

            // Markdown-Bilder → Media-Dateien + HTML
            result = MarkdownImageRegex.Replace(result, match =>
            {
                var mimeType = match.Groups[2].Value;
                var base64 = match.Groups[3].Value;
                var extension = mimeType.Split('/')[1];
                var fileName = $"img_{counter}.{extension}";

                // Datei speichern
                var filePath = Path.Combine(tempDirPath, counter.ToString());
                File.WriteAllBytes(filePath, Convert.FromBase64String(base64));

                mediaDict[fileName] = counter.ToString();
                var currentIndex = counter;
                counter++;

                return $"<img src=\"{currentIndex}\">";
            });

            mediaCounter = counter;

            // Markdown → HTML
            result = result
                .Replace("\n", "<br>")
                .Replace("**", "<b>", StringComparison.Ordinal); // Vereinfacht

            // Bold Markdown richtig konvertieren
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
            return interval switch
            {
                <= 0 => 0,
                <= 1 => 1,
                <= 3 => 2,
                <= 7 => 3,
                <= 21 => 4,
                _ => 5
            };
        }

        private static int BoxIndexToInterval(int boxIndex)
        {
            return boxIndex switch
            {
                0 => 0,
                1 => 1,
                2 => 3,
                3 => 7,
                4 => 21,
                _ => 60
            };
        }

        private static async Task CreateAnkiDatabaseAsync(string dbPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            var schema = @"
                CREATE TABLE col (
                    id INTEGER PRIMARY KEY,
                    crt INTEGER NOT NULL,
                    mod INTEGER NOT NULL,
                    scm INTEGER NOT NULL,
                    ver INTEGER NOT NULL,
                    dty INTEGER NOT NULL,
                    usn INTEGER NOT NULL,
                    ls INTEGER NOT NULL,
                    conf TEXT NOT NULL,
                    models TEXT NOT NULL,
                    decks TEXT NOT NULL,
                    dconf TEXT NOT NULL,
                    tags TEXT NOT NULL
                );

                CREATE TABLE notes (
                    id INTEGER PRIMARY KEY,
                    guid TEXT NOT NULL,
                    mid INTEGER NOT NULL,
                    mod INTEGER NOT NULL,
                    usn INTEGER NOT NULL,
                    tags TEXT NOT NULL,
                    flds TEXT NOT NULL,
                    sfld TEXT NOT NULL,
                    csum INTEGER NOT NULL,
                    flags INTEGER NOT NULL,
                    data TEXT NOT NULL
                );

                CREATE TABLE cards (
                    id INTEGER PRIMARY KEY,
                    nid INTEGER NOT NULL,
                    did INTEGER NOT NULL,
                    ord INTEGER NOT NULL,
                    mod INTEGER NOT NULL,
                    usn INTEGER NOT NULL,
                    type INTEGER NOT NULL,
                    queue INTEGER NOT NULL,
                    due INTEGER NOT NULL,
                    ivl INTEGER NOT NULL,
                    factor INTEGER NOT NULL,
                    reps INTEGER NOT NULL,
                    lapses INTEGER NOT NULL,
                    left INTEGER NOT NULL,
                    odue INTEGER NOT NULL,
                    odid INTEGER NOT NULL,
                    flags INTEGER NOT NULL,
                    data TEXT NOT NULL
                );

                CREATE TABLE revlog (
                    id INTEGER PRIMARY KEY,
                    cid INTEGER NOT NULL,
                    usn INTEGER NOT NULL,
                    ease INTEGER NOT NULL,
                    ivl INTEGER NOT NULL,
                    lastIvl INTEGER NOT NULL,
                    factor INTEGER NOT NULL,
                    time INTEGER NOT NULL,
                    type INTEGER NOT NULL
                );

                CREATE TABLE graves (
                    usn INTEGER NOT NULL,
                    oid INTEGER NOT NULL,
                    type INTEGER NOT NULL
                );

                CREATE INDEX ix_cards_nid ON cards (nid);
                CREATE INDEX ix_cards_sched ON cards (did, queue, due);
                CREATE INDEX ix_notes_csum ON notes (csum);
                CREATE INDEX ix_notes_usn ON notes (usn);
                CREATE INDEX ix_cards_usn ON cards (usn);
                CREATE INDEX ix_revlog_cid ON revlog (cid);
                CREATE INDEX ix_revlog_usn ON revlog (usn);
            ";

            using var cmd = new SqliteCommand(schema, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string CreateDecksJson(Dictionary<string, long> decks)
        {
            var deckObjects = new Dictionary<string, object>();

            foreach (var kvp in decks)
            {
                deckObjects[kvp.Value.ToString()] = new
                {
                    id = kvp.Value,
                    name = kvp.Key,
                    mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    usn = -1,
                    lrnToday = new[] { 0, 0 },
                    revToday = new[] { 0, 0 },
                    newToday = new[] { 0, 0 },
                    timeToday = new[] { 0, 0 },
                    collapsed = false,
                    desc = "",
                    dyn = 0,
                    conf = 1,
                    extendNew = 10,
                    extendRev = 50
                };
            }

            // Default-Deck hinzufügen
            deckObjects["1"] = new
            {
                id = 1,
                name = "Default",
                mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                usn = -1,
                lrnToday = new[] { 0, 0 },
                revToday = new[] { 0, 0 },
                newToday = new[] { 0, 0 },
                timeToday = new[] { 0, 0 },
                collapsed = false,
                desc = "",
                dyn = 0,
                conf = 1,
                extendNew = 10,
                extendRev = 50
            };

            return JsonSerializer.Serialize(deckObjects);
        }

        private static string CreateBasicModelJson()
        {
            var models = new Dictionary<string, object>
            {
                ["1"] = new
                {
                    id = 1,
                    name = "Basic",
                    type = 0,
                    mod = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    usn = -1,
                    sortf = 0,
                    did = 1,
                    tmpls = new[]
                    {
                        new
                        {
                            name = "Card 1",
                            ord = 0,
                            qfmt = "{{Front}}",
                            afmt = "{{FrontSide}}<hr id=answer>{{Back}}",
                            did = (long?)null,
                            bqfmt = "",
                            bafmt = ""
                        }
                    },
                    flds = new[]
                    {
                        new { name = "Front", ord = 0, sticky = false, rtl = false, font = "Arial", size = 20, media = Array.Empty<string>() },
                        new { name = "Back", ord = 1, sticky = false, rtl = false, font = "Arial", size = 20, media = Array.Empty<string>() }
                    },
                    css = ".card { font-family: arial; font-size: 20px; text-align: center; color: black; background-color: white; }",
                    latexPre = "",
                    latexPost = "",
                    latexsvg = false,
                    req = new[] { new object[] { 0, "all", new[] { 0 } } },
                    tags = Array.Empty<string>(),
                    vers = Array.Empty<string>()
                }
            };

            return JsonSerializer.Serialize(models);
        }

        private static async Task InsertColAsync(SqliteConnection connection, string decksJson, string modelsJson)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var conf = JsonSerializer.Serialize(new
            {
                activeDecks = new[] { 1 },
                curDeck = 1,
                newSpread = 0,
                collapseTime = 1200,
                timeLim = 0,
                estTimes = true,
                dueCounts = true,
                curModel = "1",
                nextPos = 1,
                sortType = "noteFld",
                sortBackwards = false,
                addToCur = true
            });

            var dconf = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["1"] = new
                {
                    id = 1,
                    name = "Default",
                    replayq = true,
                    lapse = new { delays = new[] { 10 }, mult = 0, minInt = 1, leechFails = 8, leechAction = 0 },
                    rev = new { perDay = 200, ease4 = 1.3, fuzz = 0.05, minSpace = 1, ivlFct = 1, maxIvl = 36500, bury = false, hardFactor = 1.2 },
                    @new = new { delays = new[] { 1, 10 }, ints = new[] { 1, 4, 7 }, initialFactor = 2500, separate = true, order = 1, perDay = 20, bury = false },
                    maxTaken = 60,
                    timer = 0,
                    autoplay = true,
                    mod = 0,
                    usn = -1
                }
            });

            var sql = @"INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags)
                        VALUES (1, @crt, @mod, @scm, 11, 0, -1, 0, @conf, @models, @decks, @dconf, '{}')";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@crt", now);
            cmd.Parameters.AddWithValue("@mod", now);
            cmd.Parameters.AddWithValue("@scm", now);
            cmd.Parameters.AddWithValue("@conf", conf);
            cmd.Parameters.AddWithValue("@models", modelsJson);
            cmd.Parameters.AddWithValue("@decks", decksJson);
            cmd.Parameters.AddWithValue("@dconf", dconf);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertNoteAsync(SqliteConnection connection, long id, long mid, string flds, long mod)
        {
            var sfld = flds.Split('\x1f')[0];
            var csum = (int)(GetChecksum(sfld) & 0xFFFFFFFF);
            var guid = GenerateGuid();

            var sql = @"INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data)
                        VALUES (@id, @guid, @mid, @mod, -1, '', @flds, @sfld, @csum, 0, '')";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@guid", guid);
            cmd.Parameters.AddWithValue("@mid", mid);
            cmd.Parameters.AddWithValue("@mod", mod);
            cmd.Parameters.AddWithValue("@flds", flds);
            cmd.Parameters.AddWithValue("@sfld", sfld);
            cmd.Parameters.AddWithValue("@csum", csum);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertCardAsync(SqliteConnection connection, long id, long nid, long did,
            int ivl, int factor, int queue, int type, long mod)
        {
            var sql = @"INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data)
                        VALUES (@id, @nid, @did, 0, @mod, -1, @type, @queue, 0, @ivl, @factor, 0, 0, 0, 0, 0, 0, '')";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@nid", nid);
            cmd.Parameters.AddWithValue("@did", did);
            cmd.Parameters.AddWithValue("@mod", mod);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@queue", queue);
            cmd.Parameters.AddWithValue("@ivl", ivl);
            cmd.Parameters.AddWithValue("@factor", factor);

            await cmd.ExecuteNonQueryAsync();
        }

        private static long GetChecksum(string text)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToInt64(hash, 0);
        }

        private static string GenerateGuid()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 10).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
#endif

        #endregion

        /// <summary>
        /// Hilfsklasse für temporäres Verzeichnis mit automatischer Bereinigung.
        /// </summary>
        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"capycard_import_{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
