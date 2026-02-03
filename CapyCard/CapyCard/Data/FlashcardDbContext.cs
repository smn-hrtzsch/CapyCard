using CapyCard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace CapyCard.Data
{
    public class FlashcardDbContext : DbContext
    {
        // Diese Eigenschaften werden zu Tabellen in der Datenbank
        public DbSet<Deck> Decks { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<CardSmartScore> CardSmartScores { get; set; }
        public DbSet<LearningSession> LearningSessions { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        public string DbPath { get; }

        // For testing purposes
        public static string? OverrideDbPath { get; set; }

        public FlashcardDbContext()
        {
            if (OverrideDbPath != null)
            {
                DbPath = OverrideDbPath;
                return;
            }

            if (OperatingSystem.IsBrowser())
            {
                DbPath = "InMemory";
                return;
            }

            // Wir nutzen LocalApplicationData für alle Plattformen, aber in einem sauberen Unterordner 'CapyCard'.
            // Auf Mobile ist dies ein interner Pfad, auf Desktop ~/.local/share/CapyCard/ oder %LOCALAPPDATA%/CapyCard/
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var basePath = Environment.GetFolderPath(folder);
            var appFolder = Path.Combine(basePath, "CapyCard");
            
            // Wichtig: Sicherstellen, dass der Ordner existiert!
            Directory.CreateDirectory(appFolder);
            
            DbPath = Path.Join(appFolder, "flashcards.db");
        }

        // Konfiguriert EF Core, um unsere SQLite-Datenbank am o.g. Pfad zu nutzen
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (OperatingSystem.IsBrowser())
            {
                options.UseInMemoryDatabase("FlashcardDb");
            }
            else
            {
                options.UseSqlite($"Data Source={DbPath}");
            }
        }        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Deck>()
                .HasOne(d => d.ParentDeck)
                .WithMany(d => d.SubDecks)
                .HasForeignKey(d => d.ParentDeckId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}