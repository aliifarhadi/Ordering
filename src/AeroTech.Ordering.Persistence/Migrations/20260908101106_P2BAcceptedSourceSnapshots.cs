using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2BAcceptedSourceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderItemCommercialTermsSnapshots",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    IsRefundable = table.Column<bool>(type: "bit", nullable: false),
                    IsChangeable = table.Column<bool>(type: "bit", nullable: false),
                    IsUpgradable = table.Column<bool>(type: "bit", nullable: false),
                    CheckedBaggageWeight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CheckedBaggageUnit = table.Column<int>(type: "int", nullable: true),
                    CheckedBaggagePieces = table.Column<int>(type: "int", nullable: true),
                    CabinBaggageWeight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CabinBaggageUnit = table.Column<int>(type: "int", nullable: true),
                    CabinBaggagePieces = table.Column<int>(type: "int", nullable: true),
                    PolicySource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceRuleReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TermsCapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemCommercialTermsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemCommercialTermsSnapshots_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "Order",
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemProductSnapshots",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    ProductType = table.Column<int>(type: "int", nullable: false),
                    SourceProductReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MarketingAirlineId = table.Column<int>(type: "int", nullable: true),
                    OperatingAirlineId = table.Column<int>(type: "int", nullable: true),
                    SupplierCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceOfferId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePricingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemProductSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemProductSnapshots_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "Order",
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemCommercialTermsSnapshots_OrderItemId",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemProductSnapshots_OrderItemId",
                schema: "Order",
                table: "OrderItemProductSnapshots",
                column: "OrderItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItemCommercialTermsSnapshots",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderItemProductSnapshots",
                schema: "Order");
        }
    }
}
