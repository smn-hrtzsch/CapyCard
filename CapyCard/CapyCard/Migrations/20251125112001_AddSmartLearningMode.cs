using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapyCard.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartLearningMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsRandomOrder",
                table: "LearningSessions",
                newName: "OrderMode");

            migrationBuilder.CreateTable(
                name: "CardSmartScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    LastReviewed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BoxIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSmartScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardSmartScores_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardSmartScores_CardId",
                table: "CardSmartScores",
                column: "CardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardSmartScores");

            migrationBuilder.RenameColumn(
                name: "OrderMode",
                table: "LearningSessions",
                newName: "IsRandomOrder");
        }
    }
}
