using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardApp.Migrations
{
    /// <inheritdoc />
    public partial class MoveGeneralCardsToSubDeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Find all Root Decks (ParentDeckId is NULL)
            // We use raw SQL because we can't use the DbContext here easily without dependency injection issues in migrations sometimes,
            // but simpler is to use SQL commands.
            
            // SQLite syntax
            migrationBuilder.Sql(@"
                -- Create 'Allgemein' subdecks for all root decks that don't have one yet
                INSERT INTO Decks (Name, ParentDeckId, LastLearnedCardIndex, LearnedShuffleCardIdsJson, IsRandomOrder)
                SELECT 'Allgemein', Id, 0, '[]', 0
                FROM Decks AS d
                WHERE ParentDeckId IS NULL
                AND NOT EXISTS (
                    SELECT 1 FROM Decks AS sub 
                    WHERE sub.ParentDeckId = d.Id AND sub.Name = 'Allgemein'
                );
            ");

            // Move cards from Root Deck to its 'Allgemein' subdeck
            migrationBuilder.Sql(@"
                UPDATE Cards
                SET DeckId = (
                    SELECT sub.Id 
                    FROM Decks AS sub 
                    WHERE sub.ParentDeckId = Cards.DeckId AND sub.Name = 'Allgemein'
                    LIMIT 1
                )
                WHERE DeckId IN (SELECT Id FROM Decks WHERE ParentDeckId IS NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Move cards back from 'Allgemein' subdeck to Root Deck
            migrationBuilder.Sql(@"
                UPDATE Cards
                SET DeckId = (
                    SELECT parent.Id 
                    FROM Decks AS sub 
                    JOIN Decks AS parent ON sub.ParentDeckId = parent.Id
                    WHERE sub.Id = Cards.DeckId
                )
                WHERE DeckId IN (
                    SELECT Id FROM Decks WHERE Name = 'Allgemein' AND ParentDeckId IS NOT NULL
                );
            ");

            // Delete 'Allgemein' subdecks if they are empty (which they should be now)
            migrationBuilder.Sql(@"
                DELETE FROM Decks 
                WHERE Name = 'Allgemein' 
                AND ParentDeckId IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM Cards WHERE DeckId = Decks.Id);
            ");
        }
    }
}
