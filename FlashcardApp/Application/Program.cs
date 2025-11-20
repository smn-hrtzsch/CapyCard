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
                QuestPDF.Settings.License = LicenseType.Community;
                
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
                try
                {
                    // Führt ausstehende Migrationen aus und erstellt die Datenbank, falls sie nicht existiert.
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    LogException(ex, "DatabaseMigrationFailed");
                    
                    // Fallback: Versuchen, die Datenbank manuell zu reparieren, falls Migrationen fehlschlagen.
                    try 
                    {
                        // 1. Sicherstellen, dass die Tabellen existieren
                        db.Database.EnsureCreated();

                        // 2. Migrations-Historie reparieren, damit zukünftige Migrationen funktionieren
                        db.Database.ExecuteSqlRaw(@"
                            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                                ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                                ""ProductVersion"" TEXT NOT NULL
                            );");

                        // Markieren, dass wir auf dem aktuellen Stand sind (Hack, um EF zu beruhigen)
                        db.Database.ExecuteSqlRaw(@"
                            INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                            VALUES ('20251111110823_InitialCreate', '9.0.0');");
                        
                        db.Database.ExecuteSqlRaw(@"
                            INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                            VALUES ('20251120153557_AddLearningProgressState', '9.0.0');");

                    }
                    catch (Exception retryEx)
                    {
                        LogException(retryEx, "DatabaseRepairFailed");
                    }
                }

                // Self-Healing: Fehlende Spalten IMMER prüfen und hinzufügen.
                // Dies ist notwendig, weil LastLearnedCardIndex in keiner Migration enthalten ist
                // und Migrate() daher erfolgreich sein kann, obwohl die Spalte fehlt.
                try 
                {
                    // LastLearnedCardIndex (wurde in keiner Migration explizit hinzugefügt, fehlt daher oft)
                    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Decks"" ADD COLUMN ""LastLearnedCardIndex"" INTEGER NOT NULL DEFAULT 0;"); } catch { }

                    // IsRandomOrder (aus Migration AddLearningProgressState)
                    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Decks"" ADD COLUMN ""IsRandomOrder"" INTEGER NOT NULL DEFAULT 0;"); } catch { }

                    // LearnedShuffleCardIdsJson (aus Migration AddLearningProgressState)
                    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Decks"" ADD COLUMN ""LearnedShuffleCardIdsJson"" TEXT NOT NULL DEFAULT '[]';"); } catch { }
                }
                catch (Exception ex)
                {
                    LogException(ex, "SchemaCorrectionFailed");
                }

                // Überprüfen, ob bereits Decks vorhanden sind.
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