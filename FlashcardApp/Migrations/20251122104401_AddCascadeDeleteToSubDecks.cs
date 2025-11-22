using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDeleteToSubDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks",
                column: "ParentDeckId",
                principalTable: "Decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Decks_ParentDeckId",
                table: "Decks",
                column: "ParentDeckId",
                principalTable: "Decks",
                principalColumn: "Id");
        }
    }
}
