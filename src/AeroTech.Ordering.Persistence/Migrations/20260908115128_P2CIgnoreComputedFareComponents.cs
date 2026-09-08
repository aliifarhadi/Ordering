using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2CIgnoreComputedFareComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderFareComponents_OrderAirFareConstructions_OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents");

            migrationBuilder.DropIndex(
                name: "IX_OrderFareComponents_OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents");

            migrationBuilder.DropColumn(
                name: "OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponents_OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents",
                column: "OrderAirFareConstructionId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderFareComponents_OrderAirFareConstructions_OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents",
                column: "OrderAirFareConstructionId",
                principalSchema: "Order",
                principalTable: "OrderAirFareConstructions",
                principalColumn: "Id");
        }
    }
}
