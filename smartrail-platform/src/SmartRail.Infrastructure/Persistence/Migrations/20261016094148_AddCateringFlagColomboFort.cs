using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCateringFlagColomboFort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "station",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-4000-8000-000000000001"),
                column: "has_catering_facility",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "station",
                keyColumn: "id",
                keyValue: new Guid("00000002-0000-4000-8000-000000000001"),
                column: "has_catering_facility",
                value: false);
        }
    }
}
