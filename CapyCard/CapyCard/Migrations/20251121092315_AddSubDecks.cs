using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapyCard.Migrations
{
    /// <inheritdoc />
    public partial class AddSubDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentDeckId",
                table: "Decks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_ParentDeckId",
                table: "Decks",
                column: "ParentDeckId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks",
                column: "ParentDeckId",
                principalTable: "Decks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Decks_ParentDeckId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "ParentDeckId",
                table: "Decks");
        }
    }
}
