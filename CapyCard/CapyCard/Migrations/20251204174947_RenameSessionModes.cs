using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapyCard.Migrations
{
    /// <inheritdoc />
    public partial class RenameSessionModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderMode",
                table: "LearningSessions",
                newName: "Strategy");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "LearningSessions",
                newName: "Scope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Strategy",
                table: "LearningSessions",
                newName: "OrderMode");

            migrationBuilder.RenameColumn(
                name: "Scope",
                table: "LearningSessions",
                newName: "Mode");
        }
    }
}
