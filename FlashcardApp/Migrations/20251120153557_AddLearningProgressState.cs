using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardApp.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningProgressState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRandomOrder",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LearnedShuffleCardIdsJson",
                table: "Decks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRandomOrder",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LearnedShuffleCardIdsJson",
                table: "Decks");
        }
    }
}
