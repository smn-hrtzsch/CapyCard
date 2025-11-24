using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlashcardMobile.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeckId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedDeckIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastLearnedIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    LearnedCardIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsRandomOrder = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_DeckId",
                table: "LearningSessions",
                column: "DeckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningSessions");
        }
    }
}
