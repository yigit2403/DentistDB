using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentistDB.Migrations
{
    /// <inheritdoc />
    public partial class OperationSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchIndex",
                table: "PreviousOperations",
                type: "TEXT",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PreviousOperations_SearchIndex",
                table: "PreviousOperations",
                column: "SearchIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreviousOperations_SearchIndex",
                table: "PreviousOperations");

            migrationBuilder.DropColumn(
                name: "SearchIndex",
                table: "PreviousOperations");
        }
    }
}
