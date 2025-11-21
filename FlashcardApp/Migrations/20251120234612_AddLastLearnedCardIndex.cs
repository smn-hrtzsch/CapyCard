using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardApp.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLearnedCardIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastLearnedCardIndex",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLearnedCardIndex",
                table: "Decks");
        }
    }
}
