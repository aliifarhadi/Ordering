using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3E1AcceptedPlanEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibilityDetail",
                schema: "Order",
                table: "AcceptedChangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EligibilityOutcome",
                schema: "Order",
                table: "AcceptedChangePlans",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EligibilityDetail",
                schema: "Order",
                table: "AcceptedChangePlans");

            migrationBuilder.DropColumn(
                name: "EligibilityOutcome",
                schema: "Order",
                table: "AcceptedChangePlans");
        }
    }
}
