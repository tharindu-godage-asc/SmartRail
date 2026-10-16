using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionResolvedByName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resolved_by_name",
                table: "journey_exception",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resolved_by_name",
                table: "journey_exception");
        }
    }
}
