using FlashcardApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace FlashcardApp.Data
{
    public class FlashcardDbContext : DbContext
    {
        // Diese Eigenschaften werden zu Tabellen in der Datenbank
        public DbSet<Deck> Decks { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<LearningSession> LearningSessions { get; set; }

        public string DbPath { get; }

                        public FlashcardDbContext()
                        {
                            // Wir legen die Datenbank-Datei im Benutzerprofil-Ordner ab.
                            // Das ist der Standard-Speicherort für Anwendungsdaten.
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = Path.Join(path, "flashcards.db");
        }

        // Konfiguriert EF Core, um unsere SQLite-Datenbank am o.g. Pfad zu nutzen
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Deck>()
                .HasOne(d => d.ParentDeck)
                .WithMany(d => d.SubDecks)
                .HasForeignKey(d => d.ParentDeckId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}