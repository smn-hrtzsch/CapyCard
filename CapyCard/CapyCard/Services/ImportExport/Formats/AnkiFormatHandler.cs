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

        private static readonly Regex HtmlImageRegex = new(@"<img[^>]+src=(?:""([^""]*)""|'([^']*)'|([^""'>\s]+))[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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

        public Task<ExportResult> ExportAsync(Stream stream, ExportOptions options) => Task.FromResult(ExportResult.Failed("N/A"));

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
