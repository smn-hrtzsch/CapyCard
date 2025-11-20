using Avalonia;
using System;
using QuestPDF.Infrastructure; // NEU: QuestPDF importieren
using FlashcardApp.Data;
using FlashcardApp.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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
            // NEU: QuestPDF-Lizenz setzen (erforderlich)
            QuestPDF.Settings.License = LicenseType.Community;
            
            InitializeDatabase();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
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
                catch (Exception)
                {
                    // Falls die Migration fehlschlägt (z.B. weil die Datenbank mit EnsureCreated erstellt wurde
                    // und keine Migrationshistorie hat), versuchen wir sicherzustellen, dass die DB zumindest existiert.
                    // Das ist ein Fallback für alte Installationen.
                    db.Database.EnsureCreated();
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