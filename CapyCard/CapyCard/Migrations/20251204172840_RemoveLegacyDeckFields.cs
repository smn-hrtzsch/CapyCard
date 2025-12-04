using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapyCard.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyDeckFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATA MIGRATION: Move legacy data to LearningSessions table
            // Only if no session exists for the deck yet.
            migrationBuilder.Sql(@"
                INSERT INTO LearningSessions (
                    DeckId, 
                    Mode, 
                    SelectedDeckIdsJson, 
                    LastLearnedIndex, 
                    LearnedCardIdsJson, 
                    OrderMode, 
                    LastAccessed
                )
                SELECT 
                    Id, 
                    0, -- Mode: MainOnly (Default assumption for legacy)
                    '[]', -- SelectedDeckIdsJson
                    LastLearnedCardIndex, 
                    LearnedShuffleCardIdsJson, 
                    CASE WHEN IsRandomOrder = 1 THEN 1 ELSE 0 END, -- OrderMode: 1=Random, 0=Sequential
                    date('now') -- LastAccessed
                FROM Decks
                WHERE NOT EXISTS (
                    SELECT 1 FROM LearningSessions WHERE LearningSessions.DeckId = Decks.Id
                );
            ");

            migrationBuilder.DropColumn(
                name: "IsRandomOrder",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LastLearnedCardIndex",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LearnedShuffleCardIdsJson",
                table: "Decks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRandomOrder",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastLearnedCardIndex",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LearnedShuffleCardIdsJson",
                table: "Decks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
