using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultToDeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Decks");
        }
    }
}
