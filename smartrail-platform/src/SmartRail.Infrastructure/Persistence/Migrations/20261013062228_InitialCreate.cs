using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "language",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_language", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operations_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_line", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "station",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    has_catering_facility = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_station", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "line_translation",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_line_translation", x => new { x.line_id, x.language_code });
                    table.ForeignKey(
                        name: "fk_line_translation_language_language_code",
                        column: x => x.language_code,
                        principalTable: "language",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_line_translation_line_line_id",
                        column: x => x.line_id,
                        principalTable: "line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "train_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    train_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Regional"),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    runs_on = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unreserved_capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_train_service", x => x.id);
                    table.CheckConstraint("ck_train_service_category", "service_category IN ('Regional', 'LongDistance')");
                    table.CheckConstraint("ck_train_service_direction", "direction IN ('Outbound', 'Inbound')");
                    table.CheckConstraint("ck_train_service_type", "service_type IN ('Slow', 'Express')");
                    table.CheckConstraint("ck_train_service_unreserved", "unreserved_capacity >= 0");
                    table.ForeignKey(
                        name: "fk_train_service_line_line_id",
                        column: x => x.line_id,
                        principalTable: "line",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "line_station",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    distance_km = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_line_station", x => new { x.line_id, x.station_id });
                    table.CheckConstraint("ck_line_station_distance", "distance_km >= 0");
                    table.CheckConstraint("ck_line_station_sequence", "sequence > 0");
                    table.ForeignKey(
                        name: "fk_line_station_line_line_id",
                        column: x => x.line_id,
                        principalTable: "line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_line_station_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "station",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform", x => x.id);
                    table.UniqueConstraint("ak_platform_id_station_id", x => new { x.id, x.station_id });
                    table.ForeignKey(
                        name: "fk_platform_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "station",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "station_translation",
                columns: table => new
                {
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_station_translation", x => new { x.station_id, x.language_code });
                    table.ForeignKey(
                        name: "fk_station_translation_language_language_code",
                        column: x => x.language_code,
                        principalTable: "language",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_station_translation_station_station_id",
                        column: x => x.station_id,
                        principalTable: "station",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operating_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Unassigned"),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_delay_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    expected_next_event_by = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_overdue = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey", x => x.id);
                    table.CheckConstraint("ck_journey_status", "status IN ('Unassigned', 'Scheduled', 'Running', 'Completed')");
                    table.CheckConstraint("ck_journey_version", "version >= 0");
                    table.ForeignKey(
                        name: "fk_journey_train_services_service_id",
                        column: x => x.service_id,
                        principalTable: "train_service",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "service_stop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    scheduled_arrival = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    scheduled_departure = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    planned_platform_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arrival_report_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    collection_eligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_stop", x => x.id);
                    table.CheckConstraint("ck_service_stop_sequence", "sequence > 0");
                    table.ForeignKey(
                        name: "fk_service_stop_platform_planned_platform_id_station_id",
                        columns: x => new { x.planned_platform_id, x.station_id },
                        principalTable: "platform",
                        principalColumns: new[] { "id", "station_id" });
                    table.ForeignKey(
                        name: "fk_service_stop_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "station",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_service_stop_train_services_service_id",
                        column: x => x.service_id,
                        principalTable: "train_service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operator_assignment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_assignment", x => x.id);
                    table.CheckConstraint("ck_operator_assignment_window", "valid_to IS NULL OR valid_to > valid_from");
                    table.ForeignKey(
                        name: "fk_operator_assignment_journey_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journey",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journey_stop_state",
                columns: table => new
                {
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_stop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_arrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estimated_departure = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_arrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_departure = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    platform_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_stop_state", x => new { x.journey_id, x.service_stop_id });
                    table.CheckConstraint("ck_journey_stop_state_confirmation", "confirmation IN ('Pending', 'Reported', 'Inferred')");
                    table.ForeignKey(
                        name: "fk_journey_stop_state_journey_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journey",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_journey_stop_state_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "platform",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_journey_stop_state_service_stops_service_stop_id",
                        column: x => x.service_stop_id,
                        principalTable: "service_stop",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "train_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    journey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    service_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    source = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    journey_version_after = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_train_event", x => x.id);
                    table.CheckConstraint("ck_train_event_source", "source IN ('Operator', 'Controller', 'Simulator')");
                    table.CheckConstraint("ck_train_event_type", "event_type IN ('Departed', 'Arrived', 'DelayReported', 'ReasonVerified', 'OverrideSet', 'OverrideCleared', 'PlatformChanged', 'ReportFlagged')");
                    table.ForeignKey(
                        name: "fk_train_event_journey_journey_id",
                        column: x => x.journey_id,
                        principalTable: "journey",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_train_event_service_stop_service_stop_id",
                        column: x => x.service_stop_id,
                        principalTable: "service_stop",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "language",
                columns: new[] { "code", "is_default", "name" },
                values: new object[] { "en", true, "English" });

            migrationBuilder.InsertData(
                table: "language",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { "si", "Sinhala" },
                    { "ta", "Tamil" }
                });

            migrationBuilder.InsertData(
                table: "line",
                columns: new[] { "id", "code", "name", "operations_phone" },
                values: new object[] { new Guid("00000001-0000-4000-8000-000000000001"), "MAIN", "Main Line", "+94 11 000 0000" });

            migrationBuilder.InsertData(
                table: "station",
                columns: new[] { "id", "code", "contact_phone", "name" },
                values: new object[,]
                {
                    { new Guid("00000002-0000-4000-8000-000000000001"), "CF", null, "Colombo Fort" },
                    { new Guid("00000002-0000-4000-8000-000000000002"), "MDA", null, "Maradana" },
                    { new Guid("00000002-0000-4000-8000-000000000003"), "RGM", null, "Ragama" },
                    { new Guid("00000002-0000-4000-8000-000000000004"), "GPH", null, "Gampaha" },
                    { new Guid("00000002-0000-4000-8000-000000000005"), "VYG", null, "Veyangoda" }
                });

            migrationBuilder.InsertData(
                table: "line_station",
                columns: new[] { "line_id", "station_id", "distance_km", "sequence" },
                values: new object[,]
                {
                    { new Guid("00000001-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000001"), 0.0m, 1 },
                    { new Guid("00000001-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000002"), 2.0m, 2 },
                    { new Guid("00000001-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000003"), 13.0m, 3 },
                    { new Guid("00000001-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000004"), 28.0m, 4 },
                    { new Guid("00000001-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000005"), 38.0m, 5 }
                });

            migrationBuilder.InsertData(
                table: "line_translation",
                columns: new[] { "language_code", "line_id", "name" },
                values: new object[,]
                {
                    { "si", new Guid("00000001-0000-4000-8000-000000000001"), "ප්‍රධාන මාර්ගය" },
                    { "ta", new Guid("00000001-0000-4000-8000-000000000001"), "பிரதான பாதை" }
                });

            migrationBuilder.InsertData(
                table: "platform",
                columns: new[] { "id", "code", "station_id" },
                values: new object[,]
                {
                    { new Guid("00000003-0000-4000-8000-00000000000b"), "1", new Guid("00000002-0000-4000-8000-000000000001") },
                    { new Guid("00000003-0000-4000-8000-00000000000c"), "2", new Guid("00000002-0000-4000-8000-000000000001") },
                    { new Guid("00000003-0000-4000-8000-000000000015"), "1", new Guid("00000002-0000-4000-8000-000000000002") },
                    { new Guid("00000003-0000-4000-8000-000000000016"), "2", new Guid("00000002-0000-4000-8000-000000000002") },
                    { new Guid("00000003-0000-4000-8000-00000000001f"), "1", new Guid("00000002-0000-4000-8000-000000000003") },
                    { new Guid("00000003-0000-4000-8000-000000000020"), "2", new Guid("00000002-0000-4000-8000-000000000003") },
                    { new Guid("00000003-0000-4000-8000-000000000029"), "1", new Guid("00000002-0000-4000-8000-000000000004") },
                    { new Guid("00000003-0000-4000-8000-00000000002a"), "2", new Guid("00000002-0000-4000-8000-000000000004") },
                    { new Guid("00000003-0000-4000-8000-000000000033"), "1", new Guid("00000002-0000-4000-8000-000000000005") },
                    { new Guid("00000003-0000-4000-8000-000000000034"), "2", new Guid("00000002-0000-4000-8000-000000000005") }
                });

            migrationBuilder.InsertData(
                table: "station_translation",
                columns: new[] { "language_code", "station_id", "name" },
                values: new object[,]
                {
                    { "si", new Guid("00000002-0000-4000-8000-000000000001"), "කොළඹ කොටුව" },
                    { "ta", new Guid("00000002-0000-4000-8000-000000000001"), "கொழும்பு கோட்டை" },
                    { "si", new Guid("00000002-0000-4000-8000-000000000002"), "මරදාන" },
                    { "ta", new Guid("00000002-0000-4000-8000-000000000002"), "மருதானை" },
                    { "si", new Guid("00000002-0000-4000-8000-000000000003"), "රාගම" },
                    { "ta", new Guid("00000002-0000-4000-8000-000000000003"), "ராகம" },
                    { "si", new Guid("00000002-0000-4000-8000-000000000004"), "ගම්පහ" },
                    { "ta", new Guid("00000002-0000-4000-8000-000000000004"), "கம்பஹா" },
                    { "si", new Guid("00000002-0000-4000-8000-000000000005"), "වේයන්ගොඩ" },
                    { "ta", new Guid("00000002-0000-4000-8000-000000000005"), "வேயங்கொடை" }
                });

            migrationBuilder.InsertData(
                table: "train_service",
                columns: new[] { "id", "direction", "line_id", "runs_on", "service_type", "train_number" },
                values: new object[,]
                {
                    { new Guid("00000004-0000-4000-8000-000000000001"), "Inbound", new Guid("00000001-0000-4000-8000-000000000001"), "Weekdays", "Slow", "8058" },
                    { new Guid("00000004-0000-4000-8000-000000000002"), "Inbound", new Guid("00000001-0000-4000-8000-000000000001"), "Weekdays", "Express", "8061" }
                });

            migrationBuilder.InsertData(
                table: "service_stop",
                columns: new[] { "id", "planned_platform_id", "scheduled_arrival", "scheduled_departure", "sequence", "service_id", "station_id" },
                values: new object[] { new Guid("00000005-0000-4000-8000-00000000000b"), null, null, new TimeOnly(8, 20, 0), 1, new Guid("00000004-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000005") });

            migrationBuilder.InsertData(
                table: "service_stop",
                columns: new[] { "id", "arrival_report_required", "planned_platform_id", "scheduled_arrival", "scheduled_departure", "sequence", "service_id", "station_id" },
                values: new object[] { new Guid("00000005-0000-4000-8000-00000000000c"), true, null, new TimeOnly(8, 30, 0), new TimeOnly(8, 31, 0), 2, new Guid("00000004-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000004") });

            migrationBuilder.InsertData(
                table: "service_stop",
                columns: new[] { "id", "planned_platform_id", "scheduled_arrival", "scheduled_departure", "sequence", "service_id", "station_id" },
                values: new object[,]
                {
                    { new Guid("00000005-0000-4000-8000-00000000000d"), null, new TimeOnly(8, 42, 0), new TimeOnly(8, 43, 0), 3, new Guid("00000004-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000003") },
                    { new Guid("00000005-0000-4000-8000-00000000000e"), null, new TimeOnly(8, 50, 0), new TimeOnly(8, 51, 0), 4, new Guid("00000004-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000002") },
                    { new Guid("00000005-0000-4000-8000-00000000000f"), null, new TimeOnly(8, 55, 0), null, 5, new Guid("00000004-0000-4000-8000-000000000001"), new Guid("00000002-0000-4000-8000-000000000001") },
                    { new Guid("00000005-0000-4000-8000-000000000015"), null, null, new TimeOnly(7, 50, 0), 1, new Guid("00000004-0000-4000-8000-000000000002"), new Guid("00000002-0000-4000-8000-000000000005") },
                    { new Guid("00000005-0000-4000-8000-000000000016"), null, new TimeOnly(8, 0, 0), new TimeOnly(8, 1, 0), 2, new Guid("00000004-0000-4000-8000-000000000002"), new Guid("00000002-0000-4000-8000-000000000004") },
                    { new Guid("00000005-0000-4000-8000-000000000017"), null, new TimeOnly(8, 30, 0), null, 3, new Guid("00000004-0000-4000-8000-000000000002"), new Guid("00000002-0000-4000-8000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_journey_operating_date",
                table: "journey",
                column: "operating_date");

            migrationBuilder.CreateIndex(
                name: "ix_journey_service_id_operating_date",
                table: "journey",
                columns: new[] { "service_id", "operating_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journey_stop_state_platform_id",
                table: "journey_stop_state",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "ix_journey_stop_state_service_stop_id",
                table: "journey_stop_state",
                column: "service_stop_id");

            migrationBuilder.CreateIndex(
                name: "ux_language_default",
                table: "language",
                column: "is_default",
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_line_code",
                table: "line",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_line_station_line_id_sequence",
                table: "line_station",
                columns: new[] { "line_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_line_station_station_id",
                table: "line_station",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_line_translation_language_code",
                table: "line_translation",
                column: "language_code");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_journey",
                table: "operator_assignment",
                column: "journey_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_operator",
                table: "operator_assignment",
                column: "operator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_station_id_code",
                table: "platform",
                columns: new[] { "station_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_stop_planned_platform_id_station_id",
                table: "service_stop",
                columns: new[] { "planned_platform_id", "station_id" });

            migrationBuilder.CreateIndex(
                name: "ix_service_stop_service_id_sequence",
                table: "service_stop",
                columns: new[] { "service_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_stop_service_id_station_id",
                table: "service_stop",
                columns: new[] { "service_id", "station_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_stop_station_id",
                table: "service_stop",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_station_code",
                table: "station",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_station_translation_language_code",
                table: "station_translation",
                column: "language_code");

            migrationBuilder.CreateIndex(
                name: "ix_event_journey_occurred",
                table: "train_event",
                columns: new[] { "journey_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_train_event_journey_id_client_event_id",
                table: "train_event",
                columns: new[] { "journey_id", "client_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_train_event_service_stop_id",
                table: "train_event",
                column: "service_stop_id");

            migrationBuilder.CreateIndex(
                name: "ix_train_service_line_id",
                table: "train_service",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "ix_train_service_train_number",
                table: "train_service",
                column: "train_number",
                unique: true);

            // Append-only audit trail (D-36, NFR-17, EC-132): the database refuses UPDATE, DELETE and TRUNCATE on train_event,
            // whatever the application does. Hand-written; EF Core cannot model triggers.
            migrationBuilder.Sql("""
                CREATE FUNCTION train_event_block_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'train_event is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER train_event_no_update_delete
                    BEFORE UPDATE OR DELETE ON train_event
                    FOR EACH ROW EXECUTE FUNCTION train_event_block_change();

                CREATE TRIGGER train_event_no_truncate
                    BEFORE TRUNCATE ON train_event
                    FOR EACH STATEMENT EXECUTE FUNCTION train_event_block_change();
                """);

            // Second layer (AC-E04-02 5, NFR-10). The Compose init script gives smartrail_app default privileges on every
            // table the admin user creates; this narrows the two tables that must not be writable. If the role does not
            // exist these statements fail with "role does not exist", which is intended (rule R7).
            migrationBuilder.Sql("""
                REVOKE UPDATE, DELETE ON train_event FROM smartrail_app;
                REVOKE INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" FROM smartrail_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journey_stop_state");

            migrationBuilder.DropTable(
                name: "line_station");

            migrationBuilder.DropTable(
                name: "line_translation");

            migrationBuilder.DropTable(
                name: "operator_assignment");

            migrationBuilder.DropTable(
                name: "station_translation");

            migrationBuilder.DropTable(
                name: "train_event");

            migrationBuilder.DropTable(
                name: "language");

            migrationBuilder.DropTable(
                name: "journey");

            migrationBuilder.DropTable(
                name: "service_stop");

            migrationBuilder.DropTable(
                name: "platform");

            migrationBuilder.DropTable(
                name: "train_service");

            migrationBuilder.DropTable(
                name: "station");

            migrationBuilder.DropTable(
                name: "line");

            // Dropping train_event removed its triggers; remove the function and restore the history table's default privileges.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS train_event_block_change();");
            migrationBuilder.Sql("""GRANT INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" TO smartrail_app;""");
        }
    }
}
