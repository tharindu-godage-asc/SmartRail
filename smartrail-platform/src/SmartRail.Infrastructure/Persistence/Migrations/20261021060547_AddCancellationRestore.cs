using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationRestore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_change_log_action",
                table: "service_change_log");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "restored_at",
                table: "service_cancellation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restored_by",
                table: "service_cancellation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_change_log_action",
                table: "service_change_log",
                sql: "action in ('Created', 'Amended', 'Cancelled', 'Restored')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_service_change_log_action",
                table: "service_change_log");

            migrationBuilder.DropColumn(
                name: "restored_at",
                table: "service_cancellation");

            migrationBuilder.DropColumn(
                name: "restored_by",
                table: "service_cancellation");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_change_log_action",
                table: "service_change_log",
                sql: "action in ('Created', 'Amended', 'Cancelled')");
        }
    }
}
