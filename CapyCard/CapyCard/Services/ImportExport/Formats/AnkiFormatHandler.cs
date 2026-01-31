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
    public class AnkiFormatHandler : IFormatHandler
    {
        public string[] SupportedExtensions => new[] { ".apkg" };
        public string FormatName => "Anki";
        public string FormatDescription => "Kompatibel mit Anki.";

        public bool IsAvailable =>
#if BROWSER
            false;
#else
            true;
#endif

        private static readonly Regex HtmlImageRegex = new(@"<img[^>]+src=(?:""([^""]*)""|'([^']*)'|([^"">\s]+))[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MarkdownImageRegex = new(@"!\[([^\]]*)\]\(data:([^;]+);base64,([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex SoundRegex = new(@"\[sound:([^\]]+)\]", RegexOptions.Compiled);

        public async Task<ImportPreview> AnalyzeAsync(Stream stream, string fileName)
        {
#if BROWSER
            return ImportPreview.Failed("WASM not supported.");
#else
            try
            {
                using var tempDir = new TempDirectory();
                var apkgPath = Path.Combine(tempDir.Path, "deck.apkg");
                using (var fs = File.Create(apkgPath)) await stream.CopyToAsync(fs);
                ZipFile.ExtractToDirectory(apkgPath, tempDir.Path);
                var dbPath = GetBestDatabasePath(tempDir.Path);
                if (dbPath == null) return ImportPreview.Failed("Keine Datenbank gefunden.");
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await connection.OpenAsync();
                var preview = ImportPreview.Successful(FormatName, await GetDeckNameAsync(connection), await GetCardCountAsync(connection), await GetSubDeckCountAsync(connection));
                preview.HasMedia = true;
                return preview;
            }
            catch (Exception ex) { return ImportPreview.Failed(ex.Message); }
#endif
        }

        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
#if BROWSER
            return ImportResult.Failed("WASM not supported.");
#else
            try
            {
                using var tempDir = new TempDirectory();
                var apkgPath = Path.Combine(tempDir.Path, "deck.apkg");
                using (var fs = File.Create(apkgPath)) await stream.CopyToAsync(fs);
                ZipFile.ExtractToDirectory(apkgPath, tempDir.Path);
                var dbPath = GetBestDatabasePath(tempDir.Path);
                if (dbPath == null) return ImportResult.Failed("Keine Datenbank.");
                var mediaMap = await LoadMediaMapAsync(tempDir.Path);
                using var ankiConnection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await ankiConnection.OpenAsync();
                using var context = new FlashcardDbContext();
                var deckName = options.NewDeckName ?? await GetDeckNameAsync(ankiConnection);
                Deck targetDeck;
                if (options.Target == ImportTarget.NewDeck)
                {
                    targetDeck = new Deck { Name = deckName };
                    context.Decks.Add(targetDeck); await context.SaveChangesAsync();
                    context.Decks.Add(new Deck { Name = "Allgemein", ParentDeckId = targetDeck.Id, IsDefault = true });
                    await context.SaveChangesAsync();
                }
                else
                {
                    targetDeck = await context.Decks.Include(d => d.SubDecks).FirstOrDefaultAsync(d => d.Id == options.TargetDeckId) ?? throw new Exception("Fach nicht gefunden");
                }
                var ankiDecks = await LoadAnkiDecksAsync(ankiConnection);
                var subDeckCache = targetDeck.SubDecks.ToDictionary(sd => sd.Name.ToLowerInvariant(), sd => sd);
                int imported = 0, subDecksCreated = 0;
                using var cmd = new SqliteCommand("SELECT n.flds, c.did FROM notes n JOIN cards c ON c.nid = n.id", ankiConnection);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var flds = reader.GetString(0).Split('\x1f');
                    if (flds.Length < 2) continue;
                    var front = ConvertAnkiToMarkdown(flds[0], mediaMap, tempDir.Path);
                    var back = ConvertAnkiToMarkdown(flds[1], mediaMap, tempDir.Path);
                    var subName = GetSubDeckName(ankiDecks, reader.GetInt64(1));
                    if (!subDeckCache.TryGetValue(subName.ToLowerInvariant(), out var cardDeck))
                    {
                        cardDeck = new Deck { Name = subName, ParentDeckId = targetDeck.Id, IsDefault = subName == "Allgemein" };
                        context.Decks.Add(cardDeck); await context.SaveChangesAsync();
                        subDeckCache[subName.ToLowerInvariant()] = cardDeck; subDecksCreated++;
                    }
                    context.Cards.Add(new Card { Front = front, Back = back, DeckId = cardDeck.Id });
                    imported++;
                }
                await context.SaveChangesAsync();
                return ImportResult.Successful(imported, subDecksCreated, targetDeck.Id);
            }
            catch (Exception ex) { return ImportResult.Failed(ex.Message); }
#endif
        }

        public async Task<ExportResult> ExportAsync(Stream stream, ExportOptions options)
        {
#if BROWSER
            return ExportResult.Failed("WASM not supported.");
#else
            try
            {
                using var tempDir = new TempDirectory();
                var dbPath = Path.Combine(tempDir.Path, "collection.anki21");
                var mediaFiles = new Dictionary<string, string>();
                var ankiCards = new List<AnkiCardData>();
                int mediaIndex = 0;

                // Erstelle SQLite-Datenbank
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    await connection.OpenAsync();
                    await CreateAnkiSchemaAsync(connection);
                    
                    using var context = new FlashcardDbContext();
                    var deck = await context.Decks
                        .Include(d => d.SubDecks)
                            .ThenInclude(sd => sd.Cards)
                        .Include(d => d.Cards)
                        .FirstOrDefaultAsync(d => d.Id == options.DeckId);

                    if (deck == null)
                        return ExportResult.Failed("Fach nicht gefunden.");

                    // Sammle alle Karten basierend auf Scope
                    var allCards = new List<(Card Card, string SubDeckName)>();
                    
                    if (options.Scope == ExportScope.SelectedCards && options.SelectedCardIds?.Count > 0)
                    {
                        var selectedCards = deck.Cards.Where(c => options.SelectedCardIds.Contains(c.Id)).ToList();
                        foreach (var card in selectedCards)
                            allCards.Add((card, "Allgemein"));
                        
                        foreach (var subDeck in deck.SubDecks)
                        {
                            var subSelected = subDeck.Cards.Where(c => options.SelectedCardIds.Contains(c.Id)).ToList();
                            foreach (var card in subSelected)
                                allCards.Add((card, subDeck.Name));
                        }
                    }
                    else if (options.Scope == ExportScope.SelectedSubDecks && options.SelectedSubDeckIds?.Count > 0)
                    {
                        foreach (var subDeck in deck.SubDecks.Where(sd => options.SelectedSubDeckIds.Contains(sd.Id)))
                        {
                            foreach (var card in subDeck.Cards)
                                allCards.Add((card, subDeck.Name));
                        }
                    }
                    else
                    {
                        foreach (var card in deck.Cards)
                            allCards.Add((card, "Allgemein"));
                        
                        foreach (var subDeck in deck.SubDecks)
                        {
                            foreach (var card in subDeck.Cards)
                                allCards.Add((card, subDeck.Name));
                        }
                    }

                    if (allCards.Count == 0)
                        return ExportResult.Failed("Keine Karten zum Exportieren gefunden.");

                    // Konvertiere Karten und sammle Bilder
                    foreach (var (card, subDeckName) in allCards)
                    {
                        var front = ConvertMarkdownToAnki(card.Front, mediaIndex, tempDir.Path, mediaFiles);
                        var back = ConvertMarkdownToAnki(card.Back, mediaIndex, tempDir.Path, mediaFiles);
                        mediaIndex = mediaFiles.Count; // Aktualisiere Index basierend auf Anzahl der Dateien
                        
                        ankiCards.Add(new AnkiCardData
                        {
                            Front = front,
                            Back = back,
                            SubDeckName = subDeckName
                        });
                    }

                    // Erstelle Anki-Decks und füge Karten hinzu
                    await InsertAnkiDataAsync(connection, deck.Name, ankiCards);

                    // Erstelle collection.anki2 (Abwärtskompatibilität - leere Dummy-Datenbank)
                    var anki2Path = Path.Combine(tempDir.Path, "collection.anki2");
                    using (var anki2Connection = new SqliteConnection($"Data Source={anki2Path}"))
                    {
                        await anki2Connection.OpenAsync();
                        using var anki2Cmd = new SqliteCommand(@"
                            CREATE TABLE IF NOT EXISTS col (
                                id INTEGER PRIMARY KEY, crt INTEGER, mod INTEGER, scm INTEGER, 
                                ver INTEGER, dty INTEGER, usn INTEGER, ls INTEGER, 
                                conf TEXT, models TEXT, decks TEXT, dconf TEXT, tags TEXT
                            );
                            CREATE TABLE IF NOT EXISTS notes (
                                id INTEGER PRIMARY KEY, guid TEXT, mid INTEGER, mod INTEGER,
                                usn INTEGER, tags TEXT, flds TEXT, sfld TEXT, csum INTEGER,
                                flags INTEGER, data TEXT
                            );
                            CREATE TABLE IF NOT EXISTS cards (
                                id INTEGER PRIMARY KEY, nid INTEGER, did INTEGER, ord INTEGER,
                                mod INTEGER, usn INTEGER, type INTEGER, queue INTEGER,
                                due INTEGER, ivl INTEGER, factor INTEGER, reps INTEGER,
                                lapses INTEGER, left INTEGER, odue INTEGER, odid INTEGER,
                                flags INTEGER, data TEXT
                            );
                            CREATE TABLE IF NOT EXISTS revlog (
                                id INTEGER PRIMARY KEY, cid INTEGER, usn INTEGER, ease INTEGER,
                                ivl INTEGER, lastIvl INTEGER, factor INTEGER, time INTEGER,
                                type INTEGER
                            );
                            CREATE TABLE IF NOT EXISTS graves (usn INTEGER, oid INTEGER, type INTEGER);
                            INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags)
                            VALUES (1, @crt, @mod, @scm, 11, 0, -1, 0, '{}', '{}', '{}', '{}', '{}');
                        ", anki2Connection);
                        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        anki2Cmd.Parameters.AddWithValue("@crt", now / 1000);
                        anki2Cmd.Parameters.AddWithValue("@mod", now);
                        anki2Cmd.Parameters.AddWithValue("@scm", now);
                        await anki2Cmd.ExecuteNonQueryAsync();
                    }

                    // Erstelle meta Datei (Protobuf - Format Version 1 für Legacy 2)
                    var metaPath = Path.Combine(tempDir.Path, "meta");
                    await File.WriteAllBytesAsync(metaPath, new byte[] { 0x08, 0x01 });
                }

                // Erstelle media-Datei (JSON-Mapping)
                var mediaMap = mediaFiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var mediaJson = System.Text.Json.JsonSerializer.Serialize(mediaMap);
                var mediaPath = Path.Combine(tempDir.Path, "media");
                await File.WriteAllTextAsync(mediaPath, mediaJson);

                // Erstelle ZIP-Archiv (.apkg)
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // Füge Datenbank hinzu
                    archive.CreateEntryFromFile(dbPath, "collection.anki21", CompressionLevel.Optimal);
                    
                    // Füge collection.anki2 hinzu
                    var anki2Path = Path.Combine(tempDir.Path, "collection.anki2");
                    archive.CreateEntryFromFile(anki2Path, "collection.anki2", CompressionLevel.Optimal);
                    
                    // Füge meta hinzu
                    var metaPath = Path.Combine(tempDir.Path, "meta");
                    archive.CreateEntryFromFile(metaPath, "meta", CompressionLevel.Optimal);
                    
                    // Füge media-Datei hinzu
                    archive.CreateEntryFromFile(mediaPath, "media", CompressionLevel.Optimal);
                    
                    // Füge alle Bilder hinzu
                    foreach (var kvp in mediaFiles)
                    {
                        var filePath = Path.Combine(tempDir.Path, kvp.Key);
                        if (File.Exists(filePath))
                        {
                            archive.CreateEntryFromFile(filePath, kvp.Key, CompressionLevel.Optimal);
                        }
                    }
                }

                return ExportResult.SuccessfulWithData(Array.Empty<byte>(), ankiCards.Count, 0);
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"Fehler beim Export: {ex.Message}");
            }
#endif
        }

        private async Task CreateAnkiSchemaAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS col (
                    id INTEGER PRIMARY KEY,
                    crt INTEGER NOT NULL,
                    mod INTEGER NOT NULL,
                    scm INTEGER NOT NULL,
                    ver INTEGER NOT NULL DEFAULT 11,
                    dty INTEGER NOT NULL DEFAULT 0,
                    usn INTEGER NOT NULL DEFAULT 0,
                    ls INTEGER NOT NULL DEFAULT 0,
                    conf TEXT NOT NULL,
                    models TEXT NOT NULL,
                    decks TEXT NOT NULL,
                    dconf TEXT NOT NULL,
                    tags TEXT NOT NULL DEFAULT '{}'
                );

                CREATE TABLE IF NOT EXISTS notes (
                    id INTEGER PRIMARY KEY,
                    guid TEXT NOT NULL,
                    mid INTEGER NOT NULL,
                    mod INTEGER NOT NULL,
                    usn INTEGER NOT NULL DEFAULT -1,
                    tags TEXT NOT NULL,
                    flds TEXT NOT NULL,
                    sfld TEXT NOT NULL,
                    csum INTEGER,
                    flags INTEGER DEFAULT 0,
                    data TEXT DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS cards (
                    id INTEGER PRIMARY KEY,
                    nid INTEGER NOT NULL,
                    did INTEGER NOT NULL,
                    ord INTEGER NOT NULL DEFAULT 0,
                    mod INTEGER NOT NULL,
                    usn INTEGER NOT NULL DEFAULT -1,
                    type INTEGER DEFAULT 0,
                    queue INTEGER DEFAULT 0,
                    due INTEGER DEFAULT 0,
                    ivl INTEGER DEFAULT 0,
                    factor INTEGER DEFAULT 0,
                    reps INTEGER DEFAULT 0,
                    lapses INTEGER DEFAULT 0,
                    left INTEGER DEFAULT 0,
                    odue INTEGER DEFAULT 0,
                    odid INTEGER DEFAULT 0,
                    flags INTEGER DEFAULT 0,
                    data TEXT DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS revlog (
                    id INTEGER PRIMARY KEY,
                    cid INTEGER NOT NULL,
                    usn INTEGER NOT NULL DEFAULT -1,
                    ease INTEGER NOT NULL,
                    ivl INTEGER NOT NULL,
                    lastIvl INTEGER NOT NULL,
                    factor INTEGER NOT NULL,
                    time INTEGER NOT NULL,
                    type INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS graves (
                    usn INTEGER NOT NULL,
                    oid INTEGER NOT NULL,
                    type INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_notes_usn ON notes(usn);
                CREATE INDEX IF NOT EXISTS idx_notes_mid ON notes(mid);
                CREATE INDEX IF NOT EXISTS idx_cards_usn ON cards(usn);
                CREATE INDEX IF NOT EXISTS idx_cards_nid ON cards(nid);
                CREATE INDEX IF NOT EXISTS idx_cards_odid ON cards(odid) WHERE odid != 0;
                CREATE INDEX IF NOT EXISTS idx_revlog_usn ON revlog(usn);
            ", connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsertAnkiDataAsync(SqliteConnection connection, string deckName, List<AnkiCardData> cards)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nowSeconds = now / 1000;
            
            // Verwende sichere, kleine IDs (int32 Bereich) um Überläufe zu vermeiden
            // Anki IDs sind normalerweise Zeitstempel, aber für Import können kleinere Werte sicherer sein
            // wenn irgendwo veraltete 32-bit Logik greift.
            var rnd = new Random();
            long deckId = rnd.Next(100000000, 200000000); 
            long modelId = rnd.Next(200000000, 300000000);
            
            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = null
            };
            
            // Conf - Minimal
            var confDict = new Dictionary<string, object>
            {
                ["activeDecks"] = new[] { 1 }, // Setze auf Default Deck 1 um sicher zu gehen
                ["curDeck"] = 1,
                ["newSpread"] = 0,
                ["collapseTime"] = 1200,
                ["timeLim"] = 0,
                ["estTimes"] = true,
                ["dueCounts"] = true,
                ["curModel"] = modelId.ToString(),
                ["nextPos"] = 1,
                ["sortType"] = "noteFld",
                ["sortBackwards"] = false,
                ["addToCur"] = true,
                ["dayLearnFirst"] = false,
                ["schedVer"] = 2,
                ["sched2021"] = true,
                ["creationOffset"] = 0
            };
            var confJson = JsonSerializer.Serialize(confDict, jsonOptions);

            // Models
            var modelDict = new Dictionary<string, object>
            {
                [modelId.ToString()] = new Dictionary<string, object>
                {
                    ["id"] = modelId,
                    ["name"] = "Basis",
                    ["type"] = 0,
                    ["mod"] = nowSeconds,
                    ["usn"] = -1,
                    ["sortf"] = 0,
                    ["did"] = deckId, 
                    ["tmpls"] = new[] 
                    { 
                        new Dictionary<string, object> {
                            ["name"] = "Karte 1",
                            ["ord"] = 0,
                            ["qfmt"] = "{{Vorderseite}}",
                            ["afmt"] = "{{Vorderseite}}<hr id=answer>{{Rückseite}}",
                            ["bqfmt"] = "",
                            ["bafmt"] = "",
                            ["did"] = null!
                        } 
                    },
                    ["flds"] = new[] 
                    { 
                        new Dictionary<string, object> { ["name"] = "Vorderseite", ["ord"] = 0, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20, ["media"] = new object[0] },
                        new Dictionary<string, object> { ["name"] = "Rückseite", ["ord"] = 1, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20, ["media"] = new object[0] }
                    },
                    ["css"] = ".card { font-family: arial; font-size: 20px; text-align: center; color: black; background-color: white; }",
                    ["req"] = new object[] { new object[] { 0, "all", new[] { 0 } } },
                    ["tags"] = new object[0],
                    ["vers"] = new object[0],
                    ["latexPre"] = "\\documentclass[12pt]{article}\n\\special{papersize=3in,5in}\n\\usepackage[utf8]{inputenc}\n\\usepackage{amssymb,amsmath}\n\\pagestyle{empty}\n\\setlength{\\parindent}{0in}\n\\begin{document}\n",
                    ["latexPost"] = "\\end{document}",
                    ["latexsvg"] = false
                }
            };
            var modelsJson = JsonSerializer.Serialize(modelDict, jsonOptions);

            // Decks
            var deckDict = new Dictionary<string, object>
            {
                ["1"] = new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["name"] = "Default",
                    ["desc"] = "",
                    ["mod"] = nowSeconds,
                    ["usn"] = 0,
                    ["collapsed"] = false,
                    ["browserCollapsed"] = false,
                    ["newToday"] = new object[] { 0, 0 },
                    ["revToday"] = new object[] { 0, 0 },
                    ["lrnToday"] = new object[] { 0, 0 },
                    ["timeToday"] = new object[] { 0, 0 },
                    ["dyn"] = 0,
                    ["conf"] = 1,
                    ["extendNew"] = 10,
                    ["extendRev"] = 50
                },
                [deckId.ToString()] = new Dictionary<string, object>
                {
                    ["id"] = deckId,
                    ["name"] = deckName,
                    ["desc"] = "",
                    ["mod"] = nowSeconds,
                    ["usn"] = -1,
                    ["collapsed"] = false,
                    ["browserCollapsed"] = false,
                    ["newToday"] = new object[] { 0, 0 },
                    ["revToday"] = new object[] { 0, 0 },
                    ["lrnToday"] = new object[] { 0, 0 },
                    ["timeToday"] = new object[] { 0, 0 },
                    ["dyn"] = 0,
                    ["conf"] = 1,
                    ["extendNew"] = 10,
                    ["extendRev"] = 50
                }
            };
            var decksJson = JsonSerializer.Serialize(deckDict, jsonOptions);

            // Dconf - Manuell als String für Float-Sicherheit
            var dconfJson = "{\"1\":{\"id\":1,\"name\":\"Standard\",\"mod\":" + nowSeconds + ",\"usn\":-1,\"maxTaken\":60,\"autoplay\":true,\"timer\":0,\"replayq\":true,\"new\":{\"bury\":false,\"delays\":[1.0,10.0],\"initialFactor\":2500,\"ints\":[1,4,0],\"order\":1,\"perDay\":20},\"rev\":{\"bury\":false,\"ease4\":1.3,\"hardFactor\":1.2,\"ivlFct\":1.0,\"maxIvl\":36500,\"perDay\":200},\"lapse\":{\"delays\":[10.0],\"leechAction\":1,\"leechFails\":8,\"minInt\":1,\"mult\":0.0},\"dyn\":false}}";

            using (var cmd = new SqliteCommand(@"
                INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags)
                VALUES (@id, @crt, @mod, @scm, 11, 0, 0, 0, @conf, @models, @decks, @dconf, '{}')
            ", connection))
            {
                cmd.Parameters.AddWithValue("@id", 1); // Col ID immer 1
                cmd.Parameters.AddWithValue("@crt", nowSeconds);
                cmd.Parameters.AddWithValue("@mod", now);
                cmd.Parameters.AddWithValue("@scm", now);
                cmd.Parameters.AddWithValue("@conf", confJson);
                cmd.Parameters.AddWithValue("@models", modelsJson);
                cmd.Parameters.AddWithValue("@decks", decksJson);
                cmd.Parameters.AddWithValue("@dconf", dconfJson);
                await cmd.ExecuteNonQueryAsync();
            }

            long noteId = rnd.Next(300000000, 400000000);
            long cardId = rnd.Next(400000000, 500000000);
            int dueCounter = 1;
            
            foreach (var card in cards)
            {
                noteId++;
                cardId++;
                
                var flds = card.Front + "\x1f" + card.Back;
                var sfld = Regex.Replace(card.Front, "<.*?>", ""); 
                
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data)
                    VALUES (@id, @guid, @mid, @mod, -1, '', @flds, @sfld, @csum, 0, '')
                ", connection))
                {
                    cmd.Parameters.AddWithValue("@id", noteId);
                    cmd.Parameters.AddWithValue("@guid", Guid.NewGuid().ToString("N").Substring(0, 10));
                    cmd.Parameters.AddWithValue("@mid", modelId);
                    cmd.Parameters.AddWithValue("@mod", nowSeconds);
                    cmd.Parameters.AddWithValue("@flds", flds);
                    cmd.Parameters.AddWithValue("@sfld", sfld);
                    cmd.Parameters.AddWithValue("@csum", GetCrc32(sfld));
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqliteCommand(@"
                    INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data)
                    VALUES (@id, @nid, @did, 0, @mod, -1, 0, 0, @due, 0, 0, 0, 0, 0, 0, 0, 0, '')
                ", connection))
                {
                    cmd.Parameters.AddWithValue("@id", cardId);
                    cmd.Parameters.AddWithValue("@nid", noteId);
                    cmd.Parameters.AddWithValue("@did", deckId);
                    cmd.Parameters.AddWithValue("@mod", nowSeconds);
                    cmd.Parameters.AddWithValue("@due", dueCounter++); 
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private string ConvertMarkdownToAnki(string markdown, int mediaIndex, string tempDir, Dictionary<string, string> mediaFiles)
        {
            if (string.IsNullOrEmpty(markdown))
                return "";

            var result = markdown;
            var localIndex = mediaIndex;

            result = MarkdownImageRegex.Replace(result, m =>
            {
                var altText = m.Groups[1].Value;
                var mimeType = m.Groups[2].Value;
                var base64Data = m.Groups[3].Value;

                try
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    var extension = mimeType switch
                    {
                        "image/jpeg" => "jpg",
                        "image/png" => "png",
                        "image/gif" => "gif",
                        "image/webp" => "webp",
                        _ => "jpg"
                    };

                    var fileName = $"{localIndex}.{extension}";
                    var filePath = Path.Combine(tempDir, fileName);
                    File.WriteAllBytes(filePath, bytes);

                    mediaFiles[fileName] = fileName;
                    localIndex++;

                    return $"<img src=\"{fileName}\">";
                }
                catch
                {
                    return m.Value;
                }
            });

            result = result.Replace("**", "<b>");
            result = result.Replace("*", "<i>");
            result = result.Replace("_", "<i>");
            result = result.Replace("\n", "<br>");

            return result;
        }

        private static long GetCrc32(string input)
        {
            // Korrekte CRC32 Implementierung für Anki
            var bytes = Encoding.UTF8.GetBytes(input);
            uint crc = 0xffffffff;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xedb88320;
                    else
                        crc >>= 1;
                }
            }
            return (long)(~crc); // Als long zurückgeben für DB
        }

        private class AnkiCardData
        {
            public string Front { get; set; } = "";
            public string Back { get; set; } = "";
            public string SubDeckName { get; set; } = "";
        }

        private static async Task<string> GetDeckNameAsync(SqliteConnection connection)
        {
            var decks = await LoadAnkiDecksAsync(connection);
            var userDecks = decks.Where(d => d.Key != 1 && d.Value != "Default").ToList();
            if (userDecks.Count == 0) return "Anki Import";
            var prefix = GetCommonPrefix(userDecks.Select(d => d.Value).ToList());
            var parts = (!string.IsNullOrEmpty(prefix) ? prefix : userDecks[0].Value).Split(new[] { "::", " ☰ ", "\x1f" }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts.Last() : "Anki Import";
        }

        private static string GetCommonPrefix(List<string> s)
        {
            if (s.Count == 0) return "";
            var p = s[0];
            foreach (var x in s.Skip(1)) { while (!x.StartsWith(p)) { p = p.Substring(0, p.Length - 1); if (p == "") return ""; } }
            return p.TrimEnd(':', ' ', '\x1f');
        }

        private static async Task<int> GetCardCountAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM cards", connection);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private static async Task<int> GetSubDeckCountAsync(SqliteConnection connection)
        {
            var decks = await LoadAnkiDecksAsync(connection);
            return decks.Count(d => d.Key != 1 && d.Value != "Default");
        }

        private static async Task<bool> HasProgressDataAsync(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM cards WHERE ivl > 0", connection);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async Task<Dictionary<string, string>> LoadMediaMapAsync(string tempDirPath)
        {
            var mediaPath = Path.Combine(tempDirPath, "media");
            if (!File.Exists(mediaPath)) return new Dictionary<string, string>();
            var bytes = await File.ReadAllBytesAsync(mediaPath);
            var binMap = new Dictionary<string, string>();
            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                if (json.TrimStart().StartsWith("{")) binMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch { }
            if (binMap.Count == 0)
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    using var output = new MemoryStream();
                    using (var decompressor = new DecompressionStream(ms)) decompressor.CopyTo(output);
                    var decBytes = output.ToArray();
                    var decStr = Encoding.UTF8.GetString(decBytes.Select(b => b < 32 && b != 10 && b != 13 ? (byte)32 : b).ToArray());
                    var names = Regex.Matches(decStr, @"paste-[a-f0-9]+\.jpg", RegexOptions.IgnoreCase).Select(m => m.Value).Distinct().ToList();
                    var zipFiles = Directory.GetFiles(tempDirPath, "*", SearchOption.AllDirectories).Select(Path.GetFileName).Where(n => n != null && !n.Contains("anki2") && n != "media" && n != "meta" && n != "deck.apkg").Cast<string>().ToList();
                    if (zipFiles.Count > 0)
                    {
                        var allZipFiles = zipFiles.OrderBy(n => int.TryParse(n, out var val) ? val : 999999).ToList();
                        foreach (var name in names)
                        {
                            var idx = decStr.IndexOf(name);
                            if (idx >= 0)
                            {
                                var search = decStr.Substring(Math.Max(0, idx - 15), Math.Min(decStr.Length - idx + 15, 40));
                                var idM = Regex.Match(search, @"\d+");
                                if (idM.Success && allZipFiles.Contains(idM.Value)) binMap[idM.Value] = name;
                            }
                        }
                        var unNames = names.Where(n => !binMap.Values.Contains(n)).ToList();
                        var unZip = allZipFiles.Where(z => !binMap.ContainsKey(z)).ToList();
                        for (int i = 0; i < Math.Min(unNames.Count, unZip.Count); i++) binMap[unZip[i]] = unNames[i];
                    }
                }
                catch { }
            }
            foreach (var f in Directory.GetFiles(tempDirPath, "*", SearchOption.AllDirectories))
            {
                var n = Path.GetFileName(f); if (n != null && !binMap.ContainsKey(n)) binMap[n] = n;
            }
            return binMap;
        }

        private static async Task<Dictionary<long, string>> LoadAnkiDecksAsync(SqliteConnection connection)
        {
            var decks = new Dictionary<long, string>();
            try
            {
                using var cmdNew = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='decks'", connection);
                if (await cmdNew.ExecuteScalarAsync() != null)
                {
                    using var cmd = new SqliteCommand("SELECT id, name FROM decks", connection);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync()) decks[r.GetInt64(0)] = r.GetString(1);
                }
                if (decks.Count == 0)
                {
                    using var cmd = new SqliteCommand("SELECT decks FROM col LIMIT 1", connection);
                    var json = await cmd.ExecuteScalarAsync() as string;
                    if (!string.IsNullOrEmpty(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (long.TryParse(prop.Name, out var id))
                            {
                                var nProp = prop.Value.EnumerateObject().FirstOrDefault(p => p.Name.Equals("name", StringComparison.OrdinalIgnoreCase));
                                decks[id] = nProp.Value.ValueKind == JsonValueKind.String ? nProp.Value.GetString() ?? "Default" : $"Deck {id}";
                            }
                        }
                    }
                }
            }
            catch { }
            return decks;
        }

        private static string GetSubDeckName(Dictionary<long, string> decks, long id)
        {
            if (decks.TryGetValue(id, out var name))
            {
                var parts = name.Split(new[] { "::", " ☰ ", "\x1f" }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? (parts.Last() == "Default" ? "Allgemein" : parts.Last()) : "Allgemein";
            }
            return $"Deck {id}";
        }

        private static string ConvertAnkiToMarkdown(string html, Dictionary<string, string> mediaMap, string tempDirPath)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var res = html.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            res = HtmlImageRegex.Replace(res, m =>
            {
                var orig = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                var src = System.Net.WebUtility.UrlDecode(orig);
                string? key = null;
                foreach (var e in mediaMap)
                {
                    if (e.Value == src || e.Value == orig || System.Net.WebUtility.UrlDecode(e.Value) == src) { key = e.Key; break; }
                }
                if (!string.IsNullOrEmpty(key))
                {
                    var path = Path.Combine(tempDirPath, key);
                    if (!File.Exists(path))
                    {
                        var fs = Directory.GetFiles(tempDirPath, key, SearchOption.AllDirectories);
                        if (fs.Length > 0) path = fs[0];
                    }
                    if (File.Exists(path))
                    {
                        var b = File.ReadAllBytes(path);
                        if (b.Length > 4 && b[0] == 0x28 && b[1] == 0xB5 && b[2] == 0x2F && b[3] == 0xFD)
                        {
                            try
                            {
                                using var ms = new MemoryStream(b);
                                using var outMs = new MemoryStream();
                                using (var dec = new DecompressionStream(ms)) dec.CopyTo(outMs);
                                b = outMs.ToArray();
                            }
                            catch { }
                        }
                        var mappedName = mediaMap.ContainsKey(key) ? mediaMap[key] : src;
                        return $"![Bild](data:{GetMimeType(mappedName)};base64,{Convert.ToBase64String(b)})";
                    }
                }
                return $"[Bild: {src}]";
            });
            res = SoundRegex.Replace(res, "");
            res = res.Replace("<div>", "\n").Replace("</div>", "").Replace("<p>", "\n").Replace("</p>", "\n").Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
            res = res.Replace("<ul>", "\n").Replace("</ul>", "\n").Replace("<ol>", "\n").Replace("</ol>", "\n").Replace("<li>", "\n- ").Replace("</li>", "");
            res = res.Replace("<b>", "**").Replace("</b>", "**").Replace("<strong>", "**").Replace("</strong>", "**").Replace("<i>", "_").Replace("</i>", "_").Replace("<em>", "_").Replace("</em>", "_");
            res = System.Net.WebUtility.HtmlDecode(res);
            res = Regex.Replace(res, @"<[^>]+>", "");
            res = Regex.Replace(res, @"[ \t]+", " ");
            res = Regex.Replace(res, @"\n{3,}", "\n\n");
            return res.Trim();
        }

        private static string GetMimeType(string n) => Path.GetExtension(n).ToLower() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/jpeg" };

        private static string? GetBestDatabasePath(string d)
        {
            var p1 = Path.Combine(d, "collection.anki21b");
            if (File.Exists(p1))
            {
                var t = Path.Combine(d, "db_dec");
                using (var s = File.OpenRead(p1))
                using (var o = File.Create(t))
                using (var z = new DecompressionStream(s))
                    z.CopyTo(o);
                return t;
            }
            var p2 = Path.Combine(d, "collection.anki21"); if (File.Exists(p2)) return p2;
            var p3 = Path.Combine(d, "collection.anki2"); return File.Exists(p3) ? p3 : null;
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }
            public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"capy_{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
            public void Dispose() { try { if (Directory.Exists(Path)) Directory.Delete(Path, true); } catch { } }
        }
    }
}
