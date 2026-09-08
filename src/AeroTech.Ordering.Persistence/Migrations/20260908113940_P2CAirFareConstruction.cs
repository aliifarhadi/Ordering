using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2CAirFareConstruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderAirFareConstructions",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByChangeId = table.Column<long>(type: "bigint", nullable: false),
                    SupersedesConstructionId = table.Column<long>(type: "bigint", nullable: true),
                    ConstructionType = table.Column<int>(type: "int", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcePricingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAirFareConstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAirFareConstructions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderAirFareConstructionItems",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FareConstructionId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAirFareConstructionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAirFareConstructionItems_OrderAirFareConstructions_FareConstructionId",
                        column: x => x.FareConstructionId,
                        principalSchema: "Order",
                        principalTable: "OrderAirFareConstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFarePricingGroups",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FareConstructionId = table.Column<long>(type: "bigint", nullable: false),
                    PassengerType = table.Column<int>(type: "int", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFarePricingGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFarePricingGroups_OrderAirFareConstructions_FareConstructionId",
                        column: x => x.FareConstructionId,
                        principalSchema: "Order",
                        principalTable: "OrderAirFareConstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFarePricingGroupTravellers",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PricingGroupId = table.Column<long>(type: "bigint", nullable: false),
                    OrderTravellerId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFarePricingGroupTravellers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFarePricingGroupTravellers_OrderFarePricingGroups_PricingGroupId",
                        column: x => x.PricingGroupId,
                        principalSchema: "Order",
                        principalTable: "OrderFarePricingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFarePricingUnits",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PricingGroupId = table.Column<long>(type: "bigint", nullable: false),
                    PricingUnitType = table.Column<int>(type: "int", nullable: true),
                    CombinationMethod = table.Column<int>(type: "int", nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFarePricingUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFarePricingUnits_OrderFarePricingGroups_PricingGroupId",
                        column: x => x.PricingGroupId,
                        principalSchema: "Order",
                        principalTable: "OrderFarePricingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFareComponents",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PricingUnitId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    OriginAirportId = table.Column<int>(type: "int", nullable: true),
                    DestinationAirportId = table.Column<int>(type: "int", nullable: true),
                    FareBasis = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BrandCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BrandName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FareType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CabinClassId = table.Column<int>(type: "int", nullable: true),
                    RbdId = table.Column<long>(type: "bigint", nullable: true),
                    BookingClass = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FareOwnerCarrierId = table.Column<int>(type: "int", nullable: true),
                    TariffReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RuleReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RoutingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceFareReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceComponentReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OrderAirFareConstructionId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFareComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFareComponents_OrderAirFareConstructions_OrderAirFareConstructionId",
                        column: x => x.OrderAirFareConstructionId,
                        principalSchema: "Order",
                        principalTable: "OrderAirFareConstructions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderFareComponents_OrderFarePricingUnits_PricingUnitId",
                        column: x => x.PricingUnitId,
                        principalSchema: "Order",
                        principalTable: "OrderFarePricingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFareComponentSegments",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FareComponentId = table.Column<long>(type: "bigint", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFareComponentSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFareComponentSegments_OrderFareComponents_FareComponentId",
                        column: x => x.FareComponentId,
                        principalSchema: "Order",
                        principalTable: "OrderFareComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderFareComponentServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FareComponentId = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFareComponentServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFareComponentServices_OrderFareComponents_FareComponentId",
                        column: x => x.FareComponentId,
                        principalSchema: "Order",
                        principalTable: "OrderFareComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirFareConstructionItems_FareConstructionId_OrderItemId",
                schema: "Order",
                table: "OrderAirFareConstructionItems",
                columns: new[] { "FareConstructionId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirFareConstructionItems_OrderItemId",
                schema: "Order",
                table: "OrderAirFareConstructionItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirFareConstructions_OrderId",
                schema: "Order",
                table: "OrderAirFareConstructions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirFareConstructions_SupersedesConstructionId",
                schema: "Order",
                table: "OrderAirFareConstructions",
                column: "SupersedesConstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponents_OrderAirFareConstructionId",
                schema: "Order",
                table: "OrderFareComponents",
                column: "OrderAirFareConstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponents_PricingUnitId",
                schema: "Order",
                table: "OrderFareComponents",
                column: "PricingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponentSegments_FareComponentId_OrderSegmentId",
                schema: "Order",
                table: "OrderFareComponentSegments",
                columns: new[] { "FareComponentId", "OrderSegmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponentServices_FareComponentId_OrderServiceId",
                schema: "Order",
                table: "OrderFareComponentServices",
                columns: new[] { "FareComponentId", "OrderServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderFareComponentServices_OrderServiceId",
                schema: "Order",
                table: "OrderFareComponentServices",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFarePricingGroups_FareConstructionId",
                schema: "Order",
                table: "OrderFarePricingGroups",
                column: "FareConstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFarePricingGroupTravellers_OrderTravellerId",
                schema: "Order",
                table: "OrderFarePricingGroupTravellers",
                column: "OrderTravellerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFarePricingGroupTravellers_PricingGroupId_OrderTravellerId",
                schema: "Order",
                table: "OrderFarePricingGroupTravellers",
                columns: new[] { "PricingGroupId", "OrderTravellerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderFarePricingUnits_PricingGroupId",
                schema: "Order",
                table: "OrderFarePricingUnits",
                column: "PricingGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderAirFareConstructionItems",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFareComponentSegments",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFareComponentServices",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFarePricingGroupTravellers",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFareComponents",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFarePricingUnits",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderFarePricingGroups",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderAirFareConstructions",
                schema: "Order");
        }
    }
}
