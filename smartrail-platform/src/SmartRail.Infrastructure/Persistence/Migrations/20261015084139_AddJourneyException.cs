using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJourneyException : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journey_exception",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    related_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resolution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_exception", x => x.id);
                    table.CheckConstraint("ck_journey_exception_severity", "severity IN ('Critical', 'High')");
                    table.CheckConstraint("ck_journey_exception_type", "type IN ('ReasonAwaitingVerification', 'EarlyDeparture')");
                    table.ForeignKey(
                        name: "fk_journey_exception_journey_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journey",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_journey_exception_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "station",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_journey_exception_train_events_related_event_id",
                        column: x => x.related_event_id,
                        principalTable: "train_event",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_exception_open",
                table: "journey_exception",
                columns: new[] { "severity", "created_at" },
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_journey_exception_journey_id",
                table: "journey_exception",
                column: "journey_id");

            migrationBuilder.CreateIndex(
                name: "ix_journey_exception_station_id",
                table: "journey_exception",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ux_exception_event_type",
                table: "journey_exception",
                columns: new[] { "related_event_id", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journey_exception");
        }
    }
}
