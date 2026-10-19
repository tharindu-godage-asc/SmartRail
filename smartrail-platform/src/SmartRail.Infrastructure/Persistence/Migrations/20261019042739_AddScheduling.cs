using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_train_service_line_id",
                table: "train_service");

            migrationBuilder.DropIndex(
                name: "ix_train_service_train_number",
                table: "train_service");

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_from",
                table: "train_service",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2000, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_to",
                table: "train_service",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "train_service",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Weekly");

            migrationBuilder.AddColumn<Guid>(
                name: "superseded_service_id",
                table: "train_service",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "train_service",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "service_change_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_change_log", x => x.id);
                    table.CheckConstraint("ck_service_change_log_action", "action in ('Created', 'Amended', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_service_change_log_train_services_service_id",
                        column: x => x.service_id,
                        principalTable: "train_service",
                        principalColumn: "id");
                });

            migrationBuilder.UpdateData(
                table: "train_service",
                keyColumn: "id",
                keyValue: new Guid("00000004-0000-4000-8000-000000000001"),
                columns: new[] { "effective_from", "effective_to", "superseded_service_id", "version" },
                values: new object[] { new DateOnly(2000, 1, 1), null, null, 1 });

            migrationBuilder.UpdateData(
                table: "train_service",
                keyColumn: "id",
                keyValue: new Guid("00000004-0000-4000-8000-000000000002"),
                columns: new[] { "effective_from", "effective_to", "superseded_service_id", "version" },
                values: new object[] { new DateOnly(2000, 1, 1), null, null, 1 });

            migrationBuilder.UpdateData(
                table: "train_service",
                keyColumn: "id",
                keyValue: new Guid("00000004-0000-4000-8000-000000000003"),
                columns: new[] { "effective_from", "effective_to", "superseded_service_id", "version" },
                values: new object[] { new DateOnly(2000, 1, 1), null, null, 1 });

            migrationBuilder.CreateIndex(
                name: "ix_train_service_line_id_kind_effective_from",
                table: "train_service",
                columns: new[] { "line_id", "kind", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_train_service_superseded_service_id",
                table: "train_service",
                column: "superseded_service_id");

            migrationBuilder.CreateIndex(
                name: "ix_train_service_train_number_version_effective_from",
                table: "train_service",
                columns: new[] { "train_number", "version", "effective_from" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_train_service_kind",
                table: "train_service",
                sql: "kind IN ('Weekly', 'Extra')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_train_service_version",
                table: "train_service",
                sql: "version >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_service_change_log_service_id",
                table: "service_change_log",
                column: "service_id");

            migrationBuilder.AddForeignKey(
                name: "fk_train_service_train_service_superseded_service_id",
                table: "train_service",
                column: "superseded_service_id",
                principalTable: "train_service",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_train_service_train_service_superseded_service_id",
                table: "train_service");

            migrationBuilder.DropTable(
                name: "service_change_log");

            migrationBuilder.DropIndex(
                name: "ix_train_service_line_id_kind_effective_from",
                table: "train_service");

            migrationBuilder.DropIndex(
                name: "ix_train_service_superseded_service_id",
                table: "train_service");

            migrationBuilder.DropIndex(
                name: "ix_train_service_train_number_version_effective_from",
                table: "train_service");

            migrationBuilder.DropCheckConstraint(
                name: "ck_train_service_kind",
                table: "train_service");

            migrationBuilder.DropCheckConstraint(
                name: "ck_train_service_version",
                table: "train_service");

            migrationBuilder.DropColumn(
                name: "effective_from",
                table: "train_service");

            migrationBuilder.DropColumn(
                name: "effective_to",
                table: "train_service");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "train_service");

            migrationBuilder.DropColumn(
                name: "superseded_service_id",
                table: "train_service");

            migrationBuilder.DropColumn(
                name: "version",
                table: "train_service");

            migrationBuilder.CreateIndex(
                name: "ix_train_service_line_id",
                table: "train_service",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "ix_train_service_train_number",
                table: "train_service",
                column: "train_number",
                unique: true);
        }
    }
}
