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
using CapyCard.Services;
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
        private static readonly Regex AnkiMathJaxBlockRegex = new(@"\\\[(.+?)\\\]", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AnkiMathJaxInlineRegex = new(@"\\\((.+?)\\\)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AnkiLatexTagRegex = new(@"\[latex\](.+?)\[/latex\]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
                var mediaFiles = new Dictionary<string, string>();
                var ankiCards = new List<AnkiCardData>();
                string deckName = "Export";
                
                // 1. Lade Karten aus CapyCard DB und konvertiere zu HTML
                using (var context = new FlashcardDbContext())
                {
                    var deck = await context.Decks
                        .Include(d => d.SubDecks)
                            .ThenInclude(sd => sd.Cards)
                        .Include(d => d.Cards)
                        .FirstOrDefaultAsync(d => d.Id == options.DeckId);

                    if (deck == null)
                        return ExportResult.Failed("Fach nicht gefunden.");
                    
                    deckName = deck.Name;

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
                        var front = ConvertMarkdownToAnki(card.Front, tempDir.Path, mediaFiles);
                        var back = ConvertMarkdownToAnki(card.Back, tempDir.Path, mediaFiles);
                        
                        ankiCards.Add(new AnkiCardData
                        {
                            Front = front,
                            Back = back,
                            SubDeckName = subDeckName
                        });
                    }
                }

                // 2. Bereite Anki-Daten vor (IDs generieren, JSONs bauen)
                // WICHTIG: Wir nutzen dieselben Daten für beide DBs, um Konsistenz zu gewährleisten.
                var ankiExportData = GenerateAnkiData(deckName, ankiCards);

                // 3. Erstelle collection.anki21 (Haupt-DB)
                var dbPath = Path.Combine(tempDir.Path, "collection.anki21");
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    await connection.OpenAsync();
                    await CreateAnkiSchemaAsync(connection);
                    await WriteAnkiDataAsync(connection, ankiExportData);
                }

                // 4. Erstelle collection.anki2 (Legacy-DB) - Identischer Inhalt!
                // Anki benötigt auch in der Legacy-DB gültige JSON-Konfigurationen.
                var anki2Path = Path.Combine(tempDir.Path, "collection.anki2");
                using (var anki2Connection = new SqliteConnection($"Data Source={anki2Path}"))
                {
                    await anki2Connection.OpenAsync();
                    await CreateAnkiSchemaAsync(anki2Connection);
                    await WriteAnkiDataAsync(anki2Connection, ankiExportData);
                }

                // 5. Erstelle meta Datei (Protobuf - Format Version 1 für Legacy 2)
                var metaPath = Path.Combine(tempDir.Path, "meta");
                await File.WriteAllBytesAsync(metaPath, new byte[] { 0x08, 0x01 });

                // 6. Erstelle media-Datei (JSON-Mapping)
                var mediaMap = mediaFiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var mediaJson = System.Text.Json.JsonSerializer.Serialize(mediaMap);
                var mediaPath = Path.Combine(tempDir.Path, "media");
                await File.WriteAllTextAsync(mediaPath, mediaJson);

                // 7. Erstelle ZIP-Archiv (.apkg)
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    archive.CreateEntryFromFile(dbPath, "collection.anki21", CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(anki2Path, "collection.anki2", CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(metaPath, "meta", CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(mediaPath, "media", CompressionLevel.Optimal);
                    
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
            // Schema angelehnt an Anki Standard (mit NOT NULL und korrekten Typen)
            // Indizes angepasst an 'Good' Export (kein idx_notes_mid!)
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
                    sfld INTEGER NOT NULL,
                    csum INTEGER NOT NULL,
                    flags INTEGER NOT NULL DEFAULT 0,
                    data TEXT NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS cards (
                    id INTEGER PRIMARY KEY,
                    nid INTEGER NOT NULL,
                    did INTEGER NOT NULL,
                    ord INTEGER NOT NULL DEFAULT 0,
                    mod INTEGER NOT NULL,
                    usn INTEGER NOT NULL DEFAULT -1,
                    type INTEGER NOT NULL DEFAULT 0,
                    queue INTEGER NOT NULL DEFAULT 0,
                    due INTEGER NOT NULL DEFAULT 0,
                    ivl INTEGER NOT NULL DEFAULT 0,
                    factor INTEGER NOT NULL DEFAULT 0,
                    reps INTEGER NOT NULL DEFAULT 0,
                    lapses INTEGER NOT NULL DEFAULT 0,
                    left INTEGER NOT NULL DEFAULT 0,
                    odue INTEGER NOT NULL DEFAULT 0,
                    odid INTEGER NOT NULL DEFAULT 0,
                    flags INTEGER NOT NULL DEFAULT 0,
                    data TEXT NOT NULL DEFAULT ''
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

                CREATE INDEX IF NOT EXISTS ix_notes_usn ON notes (usn);
                CREATE INDEX IF NOT EXISTS ix_cards_usn ON cards (usn);
                CREATE INDEX IF NOT EXISTS ix_revlog_usn ON revlog (usn);
                CREATE INDEX IF NOT EXISTS ix_cards_nid ON cards (nid);
                CREATE INDEX IF NOT EXISTS ix_cards_sched ON cards (did, queue, due);
                CREATE INDEX IF NOT EXISTS ix_revlog_cid ON revlog (cid);
                CREATE INDEX IF NOT EXISTS ix_notes_csum ON notes (csum);
            ", connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private AnkiExportContext GenerateAnkiData(string deckName, List<AnkiCardData> cards)
        {
            var ctx = new AnkiExportContext();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nowSeconds = now / 1000;

            ctx.Crt = nowSeconds;
            ctx.Mod = now;
            
            // IDs generieren (Zeitstempel)
            long deckId = now;
            long modelId = now + 1;
            
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };

            // Conf
            var confDict = new Dictionary<string, object>
            {
                ["creationOffset"] = -60,
                ["sched2021"] = true,
                ["dayLearnFirst"] = false,
                ["nextPos"] = 1,
                ["dueCounts"] = true,
                ["schedVer"] = 2,
                ["sortBackwards"] = false,
                ["sortType"] = "noteFld",
                ["estTimes"] = true,
                ["timeLim"] = 0,
                ["collapseTime"] = 1200,
                ["curDeck"] = 1,
                ["newSpread"] = 0,
                ["activeDecks"] = new[] { 1 },
                ["curModel"] = modelId,
                ["addToCur"] = true
            };
            ctx.ConfJson = JsonSerializer.Serialize(confDict, jsonOptions);

            // Models
            var model = new Dictionary<string, object?>
            {
                ["id"] = modelId,
                ["name"] = "Basis",
                ["type"] = 0,
                ["mod"] = 0,
                ["usn"] = 0,
                ["sortf"] = 0,
                ["did"] = null,
                ["tmpls"] = new[]
                {
                    new Dictionary<string, object?> {
                        ["name"] = "Karte 1",
                        ["ord"] = 0,
                        ["qfmt"] = "{{Vorderseite}}",
                        ["afmt"] = "{{Vorderseite}}<hr id=answer>{{Rückseite}}",
                        ["bqfmt"] = "",
                        ["bafmt"] = "",
                        ["did"] = null,
                        ["bfont"] = "",
                        ["bsize"] = 0,
                        ["id"] = Random.Shared.NextInt64()
                    }
                },
                ["flds"] = new[]
                {
                    new Dictionary<string, object?> { 
                        ["name"] = "Vorderseite", ["ord"] = 0, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20, ["description"] = "", ["plainText"] = false, ["collapsed"] = false, ["excludeFromSearch"] = false, ["id"] = Random.Shared.NextInt64(), ["tag"] = null, ["preventDeletion"] = false, ["media"] = new object[0] 
                    },
                    new Dictionary<string, object?> { 
                        ["name"] = "Rückseite", ["ord"] = 1, ["sticky"] = false, ["rtl"] = false, ["font"] = "Arial", ["size"] = 20, ["description"] = "", ["plainText"] = false, ["collapsed"] = false, ["excludeFromSearch"] = false, ["id"] = Random.Shared.NextInt64(), ["tag"] = null, ["preventDeletion"] = false, ["media"] = new object[0]
                    }
                },
                ["css"] = ".card {\n    font-family: arial;\n    font-size: 20px;\n    line-height: 1.5;\n    text-align: center;\n    color: black;\n    background-color: white;\n}\n",
                ["latexPre"] = "\\documentclass[12pt]{article}\n\\special{papersize=3in,5in}\n\\usepackage[utf8]{inputenc}\n\\usepackage{amssymb,amsmath}\n\\pagestyle{empty}\n\\setlength{\\parindent}{0in}\n\\begin{document}\n",
                ["latexPost"] = "\\end{document}",
                ["latexsvg"] = false,
                ["req"] = new object[] { new object[] { 0, "any", new[] { 0 } } },
                ["originalStockKind"] = 1
            };
            var modelDict = new Dictionary<string, object> { [modelId.ToString()] = model };
            ctx.ModelsJson = JsonSerializer.Serialize(modelDict, jsonOptions);

            // Decks
            var deckConf = new Dictionary<string, object?>
            {
                ["id"] = 1,
                ["mod"] = 0,
                ["name"] = "Default",
                ["usn"] = 0,
                ["lrnToday"] = new[] { 0, 0 },
                ["revToday"] = new[] { 0, 0 },
                ["newToday"] = new[] { 0, 0 },
                ["timeToday"] = new[] { 0, 0 },
                ["collapsed"] = false,
                ["browserCollapsed"] = false,
                ["desc"] = "",
                ["dyn"] = 0,
                ["conf"] = 1,
                ["extendNew"] = 0,
                ["extendRev"] = 0,
                ["reviewLimit"] = null,
                ["newLimit"] = null,
                ["reviewLimitToday"] = null,
                ["newLimitToday"] = null,
                ["desiredRetention"] = null
            };
            
            var customDeck = new Dictionary<string, object?>
            {
                ["id"] = deckId,
                ["mod"] = 0,
                ["name"] = deckName,
                ["usn"] = 0,
                ["lrnToday"] = new[] { 0, 0 },
                ["revToday"] = new[] { 0, 0 },
                ["newToday"] = new[] { 0, 0 },
                ["timeToday"] = new[] { 0, 0 },
                ["collapsed"] = false,
                ["browserCollapsed"] = false,
                ["desc"] = "",
                ["dyn"] = 0,
                ["conf"] = 1,
                ["extendNew"] = 0,
                ["extendRev"] = 0,
                ["reviewLimit"] = null,
                ["newLimit"] = null,
                ["reviewLimitToday"] = null,
                ["newLimitToday"] = null,
                ["desiredRetention"] = null
            };

            var deckDict = new Dictionary<string, object>
            {
                ["1"] = deckConf,
                [deckId.ToString()] = customDeck
            };
            ctx.DecksJson = JsonSerializer.Serialize(deckDict, jsonOptions);

            // Dconf
            ctx.DconfJson = "{\"1\":{\"id\":1,\"mod\":0,\"name\":\"Default\",\"usn\":0,\"maxTaken\":60,\"autoplay\":true,\"timer\":0,\"replayq\":true,\"new\":{\"bury\":false,\"delays\":[1.0,10.0],\"initialFactor\":2500,\"ints\":[1,4,0],\"order\":1,\"perDay\":20},\"rev\":{\"bury\":false,\"ease4\":1.3,\"ivlFct\":1.0,\"maxIvl\":36500,\"perDay\":200,\"hardFactor\":1.2},\"lapse\":{\"delays\":[10.0],\"leechAction\":1,\"leechFails\":8,\"minInt\":1,\"mult\":0.0},\"dyn\":false,\"newMix\":0,\"newPerDayMinimum\":0,\"interdayLearningMix\":0,\"reviewOrder\":0,\"newSortOrder\":0,\"newGatherPriority\":0,\"buryInterdayLearning\":false,\"fsrsWeights\":[],\"fsrsParams5\":[],\"fsrsParams6\":[],\"desiredRetention\":0.9,\"ignoreRevlogsBeforeDate\":\"\",\"easyDaysPercentages\":[1.0,1.0,1.0,1.0,1.0,1.0,1.0],\"stopTimerOnAnswer\":false,\"secondsToShowQuestion\":0.0,\"secondsToShowAnswer\":0.0,\"questionAction\":0,\"answerAction\":0,\"waitForAudio\":true,\"sm2Retention\":0.9,\"weightSearch\":\"\"}}";

            // Cards
            long nextId = now + 1000;

            foreach (var card in cards)
            {
                long noteId = nextId++;
                long cardId = nextId++;
                
                var sfld = Regex.Replace(card.Front, "<.*?>", "");
                
                ctx.Notes.Add(new AnkiNote
                {
                    NoteId = noteId,
                    CardId = cardId,
                    Guid = Guid.NewGuid().ToString("N").Substring(0, 10),
                    ModelId = modelId,
                    DeckId = deckId,
                    Flds = card.Front + "\x1f" + card.Back,
                    Sfld = sfld,
                    Csum = GetCrc32(sfld)
                });
            }

            return ctx;
        }


        private async Task WriteAnkiDataAsync(SqliteConnection connection, AnkiExportContext data)
        {
            using (var cmd = new SqliteCommand(@"
                INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags)
                VALUES (@id, @crt, @mod, @scm, 11, 0, 0, 0, @conf, @models, @decks, @dconf, '{}')
            ", connection))
            {
                cmd.Parameters.AddWithValue("@id", 1);
                cmd.Parameters.AddWithValue("@crt", data.Crt);
                cmd.Parameters.AddWithValue("@mod", data.Mod);
                cmd.Parameters.AddWithValue("@scm", data.Mod); // scm = mod
                cmd.Parameters.AddWithValue("@conf", data.ConfJson);
                cmd.Parameters.AddWithValue("@models", data.ModelsJson);
                cmd.Parameters.AddWithValue("@decks", data.DecksJson);
                cmd.Parameters.AddWithValue("@dconf", data.DconfJson);
                await cmd.ExecuteNonQueryAsync();
            }

            int dueCounter = 1;
            var nowSeconds = data.Crt;

            using var transaction = connection.BeginTransaction();

            foreach (var note in data.Notes)
            {
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data)
                    VALUES (@id, @guid, @mid, @mod, -1, '', @flds, @sfld, @csum, 0, '')
                ", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", note.NoteId);
                    cmd.Parameters.AddWithValue("@guid", note.Guid);
                    cmd.Parameters.AddWithValue("@mid", note.ModelId);
                    cmd.Parameters.AddWithValue("@mod", nowSeconds);
                    cmd.Parameters.AddWithValue("@flds", note.Flds);
                    cmd.Parameters.AddWithValue("@sfld", note.Sfld); // Wird als INTEGER gespeichert, aber String ist ok in SQLite
                    cmd.Parameters.AddWithValue("@csum", note.Csum);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqliteCommand(@"
                    INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data)
                    VALUES (@id, @nid, @did, 0, @mod, -1, 0, 0, @due, 0, 0, 0, 0, 0, 0, 0, 0, '')
                ", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", note.CardId);
                    cmd.Parameters.AddWithValue("@nid", note.NoteId);
                    cmd.Parameters.AddWithValue("@did", note.DeckId);
                    cmd.Parameters.AddWithValue("@mod", nowSeconds);
                    cmd.Parameters.AddWithValue("@due", dueCounter++);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
        }

        private class AnkiExportContext
        {
            public long Crt { get; set; }
            public long Mod { get; set; }
            public string ConfJson { get; set; } = "{}";
            public string ModelsJson { get; set; } = "{}";
            public string DecksJson { get; set; } = "{}";
            public string DconfJson { get; set; } = "{}";
            public List<AnkiNote> Notes { get; set; } = new();
        }

        private class AnkiNote
        {
            public long NoteId { get; set; }
            public long CardId { get; set; }
            public string Guid { get; set; } = "";
            public long ModelId { get; set; }
            public long DeckId { get; set; }
            public string Flds { get; set; } = "";
            public string Sfld { get; set; } = "";
            public long Csum { get; set; }
        }

        private string ConvertMarkdownToAnki(string markdown, string tempDir, Dictionary<string, string> mediaFiles)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var document = MarkdownService.Parse(markdown);
            var html = new StringBuilder();

            foreach (var block in document.Blocks)
            {
                AppendBlockAsHtml(html, block, tempDir, mediaFiles);
            }

            return html.ToString();
        }

        private void AppendBlockAsHtml(
            StringBuilder html,
            MarkdownService.MarkdownBlock block,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            switch (block)
            {
                case MarkdownService.MarkdownBlankLineBlock:
                    html.Append("<div><br></div>");
                    break;

                case MarkdownService.MarkdownParagraphBlock paragraphBlock:
                    html.Append("<div>")
                        .Append(RenderInlineCollectionAsHtml(paragraphBlock.Inlines, tempDir, mediaFiles))
                        .Append("</div>");
                    break;

                case MarkdownService.MarkdownListBlock listBlock:
                    AppendListBlockAsHtml(html, listBlock, tempDir, mediaFiles);
                    break;

                case MarkdownService.MarkdownChecklistBlock checklistBlock:
                    AppendChecklistBlockAsHtml(html, checklistBlock, tempDir, mediaFiles);
                    break;

                case MarkdownService.MarkdownQuoteBlock quoteBlock:
                    AppendQuoteBlockAsHtml(html, quoteBlock, tempDir, mediaFiles);
                    break;

                case MarkdownService.MarkdownTableBlock tableBlock:
                    AppendTableBlockAsHtml(html, tableBlock, tempDir, mediaFiles);
                    break;

                case MarkdownService.MarkdownFormulaBlock formulaBlock:
                    var encodedFormula = System.Net.WebUtility.HtmlEncode(formulaBlock.Content);
                    html.Append("<div>\\[")
                        .Append(encodedFormula)
                        .Append("\\]</div>");
                    break;
            }
        }

        private void AppendListBlockAsHtml(
            StringBuilder html,
            MarkdownService.MarkdownListBlock listBlock,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            var tag = listBlock.IsOrdered ? "ol" : "ul";
            html.Append('<').Append(tag).Append('>');

            foreach (var item in listBlock.Items)
            {
                html.Append("<li>")
                    .Append(RenderInlineCollectionAsHtml(item.Inlines, tempDir, mediaFiles))
                    .Append("</li>");
            }

            html.Append("</").Append(tag).Append('>');
        }

        private void AppendChecklistBlockAsHtml(
            StringBuilder html,
            MarkdownService.MarkdownChecklistBlock checklistBlock,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            html.Append("<ul>");

            foreach (var item in checklistBlock.Items)
            {
                html.Append("<li>")
                    .Append(item.IsChecked ? "[x] " : "[ ] ")
                    .Append(RenderInlineCollectionAsHtml(item.Inlines, tempDir, mediaFiles))
                    .Append("</li>");
            }

            html.Append("</ul>");
        }

        private void AppendQuoteBlockAsHtml(
            StringBuilder html,
            MarkdownService.MarkdownQuoteBlock quoteBlock,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            html.Append("<blockquote>");

            foreach (var line in quoteBlock.Lines)
            {
                html.Append("<div>");

                if (line.Level > 1)
                {
                    html.Append(System.Net.WebUtility.HtmlEncode(new string('>', line.Level - 1) + " "));
                }

                html.Append(RenderInlineCollectionAsHtml(line.Inlines, tempDir, mediaFiles));
                html.Append("</div>");
            }

            html.Append("</blockquote>");
        }

        private void AppendTableBlockAsHtml(
            StringBuilder html,
            MarkdownService.MarkdownTableBlock tableBlock,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            html.Append("<table border=\"1\" style=\"border-collapse:collapse;\"><thead><tr>");

            foreach (var headerCell in tableBlock.Header)
            {
                html.Append("<th>")
                    .Append(RenderInlineCollectionAsHtml(headerCell.Inlines, tempDir, mediaFiles))
                    .Append("</th>");
            }

            html.Append("</tr></thead>");

            if (tableBlock.Rows.Count > 0)
            {
                html.Append("<tbody>");

                foreach (var row in tableBlock.Rows)
                {
                    html.Append("<tr>");
                    foreach (var cell in row)
                    {
                        html.Append("<td>")
                            .Append(RenderInlineCollectionAsHtml(cell.Inlines, tempDir, mediaFiles))
                            .Append("</td>");
                    }

                    html.Append("</tr>");
                }

                html.Append("</tbody>");
            }

            html.Append("</table>");
        }

        private string RenderInlineCollectionAsHtml(
            IReadOnlyList<MarkdownService.MarkdownInline> inlines,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            var html = new StringBuilder();

            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case MarkdownService.MarkdownTextInline textInline:
                        html.Append(RenderTextInlineAsHtml(textInline));
                        break;

                    case MarkdownService.MarkdownFormulaInline formulaInline:
                        html.Append("\\(")
                            .Append(System.Net.WebUtility.HtmlEncode(formulaInline.Content))
                            .Append("\\)");
                        break;

                    case MarkdownService.MarkdownImageInline imageInline:
                        html.Append(ConvertImageInlineToAnkiHtml(imageInline, tempDir, mediaFiles));
                        break;
                }
            }

            return html.ToString();
        }

        private static string RenderTextInlineAsHtml(MarkdownService.MarkdownTextInline textInline)
        {
            var content = System.Net.WebUtility.HtmlEncode(textInline.Text);

            if (textInline.IsHighlight)
            {
                content = "<mark>" + content + "</mark>";
            }

            if (textInline.IsUnderline)
            {
                content = "<u>" + content + "</u>";
            }

            if (textInline.IsItalic)
            {
                content = "<i>" + content + "</i>";
            }

            if (textInline.IsBold)
            {
                content = "<b>" + content + "</b>";
            }

            return content;
        }

        private string ConvertImageInlineToAnkiHtml(
            MarkdownService.MarkdownImageInline imageInline,
            string tempDir,
            Dictionary<string, string> mediaFiles)
        {
            if (string.IsNullOrWhiteSpace(imageInline.Source))
            {
                return string.Empty;
            }

            if (!TryExtractImageData(imageInline.Source, out var imageBytes, out var extension))
            {
                return System.Net.WebUtility.HtmlEncode(imageInline.AltText);
            }

            var currentIndex = mediaFiles.Count;
            var zipFileName = currentIndex.ToString();
            var contentFileName = $"image-{currentIndex}.{extension}";

            var filePath = Path.Combine(tempDir, zipFileName);
            File.WriteAllBytes(filePath, imageBytes);
            mediaFiles[zipFileName] = contentFileName;

            return $"<img src=\"{contentFileName}\">";
        }

        private static bool TryExtractImageData(string source, out byte[] bytes, out string extension)
        {
            bytes = Array.Empty<byte>();
            extension = "png";

            var dataUriMatch = Regex.Match(source, @"^data:([^;]+);base64,(.+)$", RegexOptions.IgnoreCase);
            if (dataUriMatch.Success)
            {
                try
                {
                    bytes = Convert.FromBase64String(dataUriMatch.Groups[2].Value);
                    extension = GetExtensionForMimeType(dataUriMatch.Groups[1].Value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (File.Exists(source))
            {
                bytes = File.ReadAllBytes(source);
                extension = Path.GetExtension(source).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = "png";
                }

                return true;
            }

            return false;
        }

        private static string GetExtensionForMimeType(string mimeType)
        {
            return mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/gif" => "gif",
                "image/webp" => "webp",
                "image/svg+xml" => "svg",
                _ => "png"
            };
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

            // Anki-MathJax / LaTeX-Tags zurück in Markdown-Formeln konvertieren
            res = AnkiLatexTagRegex.Replace(res, m =>
            {
                var latex = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                return "$$\n" + latex + "\n$$";
            });
            res = AnkiMathJaxBlockRegex.Replace(res, m =>
            {
                var latex = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                return "$$\n" + latex + "\n$$";
            });
            res = AnkiMathJaxInlineRegex.Replace(res, m =>
            {
                var latex = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                return "$" + latex + "$";
            });

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
