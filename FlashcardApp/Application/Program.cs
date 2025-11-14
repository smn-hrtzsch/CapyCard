using Avalonia;
using System;
using QuestPDF.Infrastructure; // NEU: QuestPDF importieren
using FlashcardApp.Data;
using FlashcardApp.Models;
using System.Linq;

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
            
            SeedDatabase();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        
        private static void SeedDatabase()
        {
            using (var db = new FlashcardDbContext())
            {
                // Stellen Sie sicher, dass die Datenbank erstellt ist.
                db.Database.EnsureCreated();

                // Überprüfen, ob bereits Decks vorhanden sind.
                if (!db.Decks.Any())
                {
                    var deck = new Deck { Name = "2 Jahre Beziehung <3" };
                    db.Decks.Add(deck);
                    db.SaveChanges();

                    db.Cards.AddRange(
                        new Card { DeckId = deck.Id, Front = "Meine liebste Erinnerung", Back = "Gemeinsam den Sonnenuntergang an unserem \"Privatstrand\" in Kroatien beobachten." },
                        new Card { DeckId = deck.Id, Front = "Was ich so an dir schätze", Back = "Deine Zielstrebigkeit \n Deine Kraft für den Alltag \n Deinen Fleiß \n Deine Liebe zur Bibel \n Deine Ehrlichkeit \n Deine Kreativität \n Deinen Mut" },
                        new Card { DeckId = deck.Id, Front = "Was ich so an dir liebe", Back = "Deine Empathie \n Deine Fürsorglichkeit \n Deine Fürsorge \n Deine Gutherzigkeit \n Deine Witze und Humor \n Deine süße Art \n Dein Lächeln" },
                        new Card { DeckId = deck.Id, Front = "Meine Lieblingsmomente", Back = "Erstes Date im Kino in Freiberg und das Kuscheln danach auf der Parkbank \n Als ich Talisma kennenlernen durfte \n Die vielen Gottesdienstbesuche \n Wenn wir zusammen im Bett kuscheln und Serien schauen \n Wenn wir uns ausgiebig umarmem" },
                        new Card { DeckId = deck.Id, Front = "Was ich an deinem Aussehen liebe", Back = "Deine wunderschönen Augen \n Dein zauberhaftes Lächeln \n Deine Haare \n Dein immer hübscher Kleidungsstil \n Alles andere an deinem Körper" },
                        new Card { DeckId = deck.Id, Front = "Was mich glücklich macht", Back = "Dass wir immer zusammenhalten, egal was ist. Dafür liebe ich dich und unsere Beziehung." },
                        new Card { DeckId = deck.Id, Front = "Was ich mir wünsche", Back = "Dass wir für immer zusammenbleiben" },
                        new Card { DeckId = deck.Id, Front = "Wofür ich Gott ganz besonders dankbar bin", Back = "Dass er dich geschaffen hat und mir an meine Seite gegeben hat" },
                        new Card { DeckId = deck.Id, Front = "Was du niemals vergessen darfst", Back = "Ich LIEBE dich über alles und das wird auch immer so bleiben!" },
                        new Card { DeckId = deck.Id, Front = "Meine Schwachstelle", Back = "Du und Talisma, weil ich ohne euch niemals mehr leben wollen würde. \n Und Natürlich Nutella pah." }
                    );
                    db.SaveChanges();
                }
            }
        }
    }
}