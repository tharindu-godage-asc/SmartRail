using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedPlannedPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000b"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-000000000033"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000c"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-000000000029"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000d"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-00000000001f"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000e"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-000000000015"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000f"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-00000000000b"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000015"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-000000000033"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000016"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-000000000029"));

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000017"),
                column: "planned_platform_id",
                value: new Guid("00000003-0000-4000-8000-00000000000b"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000b"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000c"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000d"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000e"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000000f"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000015"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000016"),
                column: "planned_platform_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000017"),
                column: "planned_platform_id",
                value: null);
        }
    }
}
