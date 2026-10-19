using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "end_note",
                table: "operator_assignment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "end_reason",
                table: "operator_assignment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ended_by",
                table: "operator_assignment",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_operator_assignment_end_reason",
                table: "operator_assignment",
                sql: "end_reason IN ('LostDevice', 'CrewChange', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operator_assignment_ended_by",
                table: "operator_assignment",
                sql: "end_reason IS NULL OR ended_by IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operator_assignment_end_reason",
                table: "operator_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operator_assignment_ended_by",
                table: "operator_assignment");

            migrationBuilder.DropColumn(
                name: "end_note",
                table: "operator_assignment");

            migrationBuilder.DropColumn(
                name: "end_reason",
                table: "operator_assignment");

            migrationBuilder.DropColumn(
                name: "ended_by",
                table: "operator_assignment");
        }
    }
}
