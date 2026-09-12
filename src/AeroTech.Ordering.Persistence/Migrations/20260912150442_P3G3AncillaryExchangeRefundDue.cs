using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G3AncillaryExchangeRefundDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundDueDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundDueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDueReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundDueDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups");

            migrationBuilder.DropColumn(
                name: "RefundDueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups");

            migrationBuilder.DropColumn(
                name: "RefundDueReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups");
        }
    }
}
