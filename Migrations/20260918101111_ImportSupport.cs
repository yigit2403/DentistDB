using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentistDB.Migrations
{
    /// <inheritdoc />
    public partial class ImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Tckn",
                table: "Patients",
                type: "TEXT",
                maxLength: 11,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 11);

            migrationBuilder.AddColumn<bool>(
                name: "IsImported",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LegacyKey",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LegacyKey",
                table: "Patients",
                column: "LegacyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_LegacyKey",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IsImported",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyKey",
                table: "Patients");

            migrationBuilder.AlterColumn<string>(
                name: "Tckn",
                table: "Patients",
                type: "TEXT",
                maxLength: 11,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 11,
                oldNullable: true);
        }
    }
}
