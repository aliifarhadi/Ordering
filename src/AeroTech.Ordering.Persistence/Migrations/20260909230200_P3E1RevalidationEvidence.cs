using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3E1RevalidationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RevalidationDetail",
                schema: "Order",
                table: "AcceptedChangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevalidationOutcome",
                schema: "Order",
                table: "AcceptedChangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevalidationProviderReference",
                schema: "Order",
                table: "AcceptedChangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevalidationDetail",
                schema: "Order",
                table: "AcceptedChangePlans");

            migrationBuilder.DropColumn(
                name: "RevalidationOutcome",
                schema: "Order",
                table: "AcceptedChangePlans");

            migrationBuilder.DropColumn(
                name: "RevalidationProviderReference",
                schema: "Order",
                table: "AcceptedChangePlans");
        }
    }
}
