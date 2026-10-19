using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_journey_status",
                table: "journey");

            migrationBuilder.CreateTable(
                name: "service_cancellation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    train_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_cancellation", x => x.id);
                    table.CheckConstraint("ck_service_cancellation_range", "to_date IS NULL OR to_date >= from_date");
                    table.ForeignKey(
                        name: "fk_service_cancellation_line_line_id",
                        column: x => x.line_id,
                        principalTable: "line",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_service_cancellation_train_services_service_id",
                        column: x => x.service_id,
                        principalTable: "train_service",
                        principalColumn: "id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_journey_status",
                table: "journey",
                sql: "status IN ('Unassigned', 'Scheduled', 'Running', 'Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_service_cancellation_line_id_train_number_from_date",
                table: "service_cancellation",
                columns: new[] { "line_id", "train_number", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_service_cancellation_service_id",
                table: "service_cancellation",
                column: "service_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_cancellation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_journey_status",
                table: "journey");

            migrationBuilder.AddCheckConstraint(
                name: "ck_journey_status",
                table: "journey",
                sql: "status IN ('Unassigned', 'Scheduled', 'Running', 'Completed')");
        }
    }
}
