using Avalonia;
using System;
using QuestPDF.Infrastructure; // NEU: QuestPDF importieren
using FlashcardApp.Data;
using FlashcardApp.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace FlashcardApp
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Global exception handler for UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
            {
                LogException(e.ExceptionObject as Exception, "UnhandledException");
            };

            try 
            {
                // NEU: QuestPDF-Lizenz setzen (erforderlich)
                // Wir fangen Fehler hier ab, da QuestPDF auf manchen Plattformen (z.B. Windows ARM64)
                // oder bei fehlenden Abhängigkeiten (VC++ Redist) abstürzen kann.
                // Die App soll trotzdem starten, nur PDF-Export geht dann halt nicht.
                try 
                {
                    QuestPDF.Settings.License = LicenseType.Community;
                }
                catch (Exception qEx)
                {
                    LogException(qEx, "QuestPDF_Initialization_Failed");
                    // Wir könnten hier ein Flag setzen, dass PDF deaktiviert ist.
                }
                
                InitializeDatabase();

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                LogException(ex, "MainCrash");
                throw; // Re-throw to let the OS handle the crash report if needed
            }
        }

        private static void LogException(Exception? ex, string source)
        {
            if (ex == null) return;
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlashcardApp_CrashLog.txt");
                var message = $"[{DateTime.Now}] [{source}] {ex.ToString()}{Environment.NewLine}";
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Failed to log... nothing we can do.
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        
        private static void InitializeDatabase()
        {
            using (var db = new FlashcardDbContext())
            {
                // 1. Inkonsistenzen beheben (Windows-Bug: Migration erledigt, aber Spalte fehlt)
                try
                {
                    var connection = db.Database.GetDbConnection();
                    connection.Open();

                    bool hasColumn = false;
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA table_info(Decks);";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.GetString(1) == "LastLearnedCardIndex")
                                {
                                    hasColumn = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!hasColumn)
                    {
                        // Spalte fehlt -> Migration aus Historie löschen, damit Migrate() sie neu anwendet
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%_AddLastLearnedCardIndex';";
                            command.ExecuteNonQuery();
                        }
                    }
                    connection.Close();
                }
                catch (Exception ex)
                {
                    LogException(ex, "PreMigrationCheckFailed");
                }

                // 2. Migrationen ausführen
                try
                {
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    LogException(ex, "DatabaseMigrationFailed");
                    // Fallback für ganz kaputte DBs (optional, aber sicher ist sicher)
                    try { db.Database.EnsureCreated(); } catch { }
                }

                // 3. Seed Data
                if (!db.Decks.Any())
                {
                    var deck = new Deck { Name = "2 Jahre Beziehung <3" };
                    db.Decks.Add(deck);
                    db.SaveChanges();

                    db.Cards.AddRange(
                        new Card { DeckId = deck.Id, Front = "Meine liebste Erinnerung", Back = "Gemeinsam den Sonnenuntergang an unserem \"Privatstrand\" in Kroatien beobachten." },
                        new Card { DeckId = deck.Id, Front = "Was ich so an dir schätze", Back = "Deine Zielstrebigkeit \nDeine Kraft für den Alltag \nDeinen Fleiß \nDeine Liebe zur Bibel \nDeine Ehrlichkeit \nDeine Kreativität \nDeinen Mut" },
                        new Card { DeckId = deck.Id, Front = "Was ich so an dir liebe", Back = "Deine Empathie \nDeine Fürsorglichkeit \nDeine Fürsorge \nDeine Gutherzigkeit \nDeine Witze und Humor \nDeine süße Art \nDein Lächeln" },
                        new Card { DeckId = deck.Id, Front = "Meine Lieblingsmomente", Back = "Erstes Date im Kino in Freiberg und das Kuscheln danach auf der Parkbank \nAls ich Talisma kennenlernen durfte \nDie vielen Gottesdienstbesuche \nWenn wir zusammen im Bett kuscheln und Serien schauen \nWenn wir uns ausgiebig umarmen" },
                        new Card { DeckId = deck.Id, Front = "Was ich an deinem Aussehen liebe", Back = "Deine wunderschönen Augen \nDein zauberhaftes Lächeln \nDeine Haare \nDein immer hübscher Kleidungsstil \nAlles andere an deinem Körper" },
                        new Card { DeckId = deck.Id, Front = "Was mich glücklich macht", Back = "Dass wir immer zusammenhalten, egal was ist. Dafür liebe ich dich und unsere Beziehung." },
                        new Card { DeckId = deck.Id, Front = "Was ich mir wünsche", Back = "Dass wir für immer zusammenbleiben" },
                        new Card { DeckId = deck.Id, Front = "Wofür ich Gott ganz besonders dankbar bin", Back = "Dass er dich geschaffen hat und mir an meine Seite gegeben hat" },
                        new Card { DeckId = deck.Id, Front = "Was du niemals vergessen darfst", Back = "Ich LIEBE dich über alles und das wird auch immer so bleiben!" },
                        new Card { DeckId = deck.Id, Front = "Meine Schwachstelle", Back = "Du und Talisma, weil ich ohne euch niemals mehr leben wollen würde. \nUnd Natürlich Nutella pah." }
                    );
                    db.SaveChanges();
                }
            }
        }
    }
}