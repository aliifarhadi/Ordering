using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2A1PricingProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_OrderId_SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<string>(
                name: "OccurrenceKey",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResidualOriginalAmount",
                schema: "Order",
                table: "OrderPricingAllocationSets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidualOriginalCurrencyId",
                schema: "Order",
                table: "OrderPricingAllocationSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResidualSaleAmount",
                schema: "Order",
                table: "OrderPricingAllocationSets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ResidualSaleCurrencyId",
                schema: "Order",
                table: "OrderPricingAllocationSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OrderId",
                schema: "Order",
                table: "OrderPricingLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_PriceChangeSetId_SourceLineRef_OccurrenceKey",
                schema: "Order",
                table: "OrderPricingLines",
                columns: new[] { "PriceChangeSetId", "SourceLineRef", "OccurrenceKey" },
                unique: true,
                filter: "[SourceLineRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_OrderId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_PriceChangeSetId_SourceLineRef_OccurrenceKey",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "OccurrenceKey",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "ResidualOriginalAmount",
                schema: "Order",
                table: "OrderPricingAllocationSets");

            migrationBuilder.DropColumn(
                name: "ResidualOriginalCurrencyId",
                schema: "Order",
                table: "OrderPricingAllocationSets");

            migrationBuilder.DropColumn(
                name: "ResidualSaleAmount",
                schema: "Order",
                table: "OrderPricingAllocationSets");

            migrationBuilder.DropColumn(
                name: "ResidualSaleCurrencyId",
                schema: "Order",
                table: "OrderPricingAllocationSets");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OrderId_SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines",
                columns: new[] { "OrderId", "SourceLineRef" },
                unique: true,
                filter: "[SourceLineRef] IS NOT NULL");
        }
    }
}
