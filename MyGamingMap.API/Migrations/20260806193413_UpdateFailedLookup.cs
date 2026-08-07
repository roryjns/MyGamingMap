using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGamingMap.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFailedLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "normalised_name",
                table: "failed_lookups",
                newName: "name");

            migrationBuilder.RenameIndex(
                name: "ix_failed_lookups_normalised_name_platform",
                table: "failed_lookups",
                newName: "ix_failed_lookups_name_platform");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "failed_lookups",
                newName: "normalised_name");

            migrationBuilder.RenameIndex(
                name: "ix_failed_lookups_name_platform",
                table: "failed_lookups",
                newName: "ix_failed_lookups_normalised_name_platform");
        }
    }
}
