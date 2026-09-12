using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G2AncillaryRefundEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundPricingLines",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(max)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundedOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundPricingLines",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundedOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
