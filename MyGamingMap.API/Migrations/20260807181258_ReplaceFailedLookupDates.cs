using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGamingMap.API.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceFailedLookupDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_attempt",
                table: "failed_lookups");

            migrationBuilder.RenameColumn(
                name: "last_attempt",
                table: "failed_lookups",
                newName: "date_added");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "date_added",
                table: "failed_lookups",
                newName: "last_attempt");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_attempt",
                table: "failed_lookups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
