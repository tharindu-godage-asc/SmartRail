using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedNightTrain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "train_service",
                columns: new[] { "id", "direction", "line_id", "runs_on", "service_type", "train_number" },
                values: new object[] { new Guid("00000004-0000-4000-8000-000000000003"), "Inbound", new Guid("00000001-0000-4000-8000-000000000001"), "Daily", "Slow", "8075" });

            migrationBuilder.InsertData(
                table: "service_stop",
                columns: new[] { "id", "planned_platform_id", "scheduled_arrival", "scheduled_departure", "sequence", "service_id", "station_id" },
                values: new object[,]
                {
                    { new Guid("00000005-0000-4000-8000-00000000001f"), new Guid("00000003-0000-4000-8000-000000000033"), null, new TimeOnly(22, 10, 0), 1, new Guid("00000004-0000-4000-8000-000000000003"), new Guid("00000002-0000-4000-8000-000000000005") },
                    { new Guid("00000005-0000-4000-8000-000000000020"), new Guid("00000003-0000-4000-8000-000000000029"), new TimeOnly(22, 20, 0), new TimeOnly(22, 21, 0), 2, new Guid("00000004-0000-4000-8000-000000000003"), new Guid("00000002-0000-4000-8000-000000000004") },
                    { new Guid("00000005-0000-4000-8000-000000000021"), new Guid("00000003-0000-4000-8000-00000000001f"), new TimeOnly(22, 32, 0), new TimeOnly(22, 33, 0), 3, new Guid("00000004-0000-4000-8000-000000000003"), new Guid("00000002-0000-4000-8000-000000000003") },
                    { new Guid("00000005-0000-4000-8000-000000000022"), new Guid("00000003-0000-4000-8000-000000000015"), new TimeOnly(22, 40, 0), new TimeOnly(22, 41, 0), 4, new Guid("00000004-0000-4000-8000-000000000003"), new Guid("00000002-0000-4000-8000-000000000002") },
                    { new Guid("00000005-0000-4000-8000-000000000023"), new Guid("00000003-0000-4000-8000-00000000000b"), new TimeOnly(22, 45, 0), null, 5, new Guid("00000004-0000-4000-8000-000000000003"), new Guid("00000002-0000-4000-8000-000000000001") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-00000000001f"));

            migrationBuilder.DeleteData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000020"));

            migrationBuilder.DeleteData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000021"));

            migrationBuilder.DeleteData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000022"));

            migrationBuilder.DeleteData(
                table: "service_stop",
                keyColumn: "id",
                keyValue: new Guid("00000005-0000-4000-8000-000000000023"));

            migrationBuilder.DeleteData(
                table: "train_service",
                keyColumn: "id",
                keyValue: new Guid("00000004-0000-4000-8000-000000000003"));
        }
    }
}
