using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G2AncillaryRefundConsequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RefundPriceChangeSetId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundPricingSource",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundPriceChangeSetId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundPricingSource",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
