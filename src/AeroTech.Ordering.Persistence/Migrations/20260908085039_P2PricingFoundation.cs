using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2PricingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderPricingLineAllocations",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_OrderId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "IsPercentage",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<string>(
                name: "TaxDetails",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "LineSubCategory",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "SaleCurrencyId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "LineScope",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "OriginalCurrencyId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "LineReason",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineRole",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "LineDirection",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "Effect",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "LineCategory",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "EquivalentCurrencyId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "ComponentType",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "EquivalentAmount",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<decimal>(
                name: "SaleAmount",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "BasisType",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AncillaryTotal",
                schema: "Order",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "FinancialSequence",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "OwnerAirlineId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationLevel",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BasisReferenceId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationSnapshot",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Order",
                table: "OrderPricingLines",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "OrderItemId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OriginalAllocationId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PriceChangeSetId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RelatedOperationId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementCategory",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementPartyRef",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferGroupId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderChanges",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ActorId = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderChanges_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPriceChangeSets",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ChangeId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialSequence = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedCommercialVersion = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceOfferId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourcePricingRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPriceChangeSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPriceChangeSets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingAllocationSets",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderPricingLineId = table.Column<long>(type: "bigint", nullable: false),
                    OrderIdAtCreation = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SupersedesAllocationSetId = table.Column<long>(type: "bigint", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Completeness = table.Column<int>(type: "int", nullable: false),
                    PricingContextRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingAllocationSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingAllocationSets_OrderPricingLines_OrderPricingLineId",
                        column: x => x.OrderPricingLineId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingAllocations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AllocationSetId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemIdAtAllocation = table.Column<long>(type: "bigint", nullable: true),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    TravellerId = table.Column<long>(type: "bigint", nullable: true),
                    ItineraryIdAtAllocation = table.Column<long>(type: "bigint", nullable: true),
                    SegmentIdAtAllocation = table.Column<long>(type: "bigint", nullable: true),
                    CoveragePortionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OriginalCurrencyId = table.Column<int>(type: "int", nullable: true),
                    SaleAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaleCurrencyId = table.Column<int>(type: "int", nullable: false),
                    RateOfExchange = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    NumberOfDecimalPlaces = table.Column<int>(type: "int", nullable: true),
                    RateOfExchangeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoundingFactor = table.Column<int>(type: "int", nullable: true),
                    OriginalAllocationId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingAllocations_OrderPricingAllocationSets_AllocationSetId",
                        column: x => x.AllocationSetId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingAllocationSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OwnerAirlineId_Id",
                schema: "Order",
                table: "Orders",
                columns: new[] { "OwnerAirlineId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OrderId_SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines",
                columns: new[] { "OrderId", "SourceLineRef" },
                unique: true,
                filter: "[SourceLineRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OriginalPricingLineId",
                schema: "Order",
                table: "OrderPricingLines",
                column: "OriginalPricingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_PriceChangeSetId",
                schema: "Order",
                table: "OrderPricingLines",
                column: "PriceChangeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChanges_OrderId_OccurredAt",
                schema: "Order",
                table: "OrderChanges",
                columns: new[] { "OrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPriceChangeSets_ChangeId",
                schema: "Order",
                table: "OrderPriceChangeSets",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPriceChangeSets_OrderId_FinancialSequence",
                schema: "Order",
                table: "OrderPriceChangeSets",
                columns: new[] { "OrderId", "FinancialSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingAllocations_AllocationSetId",
                schema: "Order",
                table: "OrderPricingAllocations",
                column: "AllocationSetId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingAllocations_OrderServiceId",
                schema: "Order",
                table: "OrderPricingAllocations",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingAllocationSets_OrderPricingLineId_Purpose_Version",
                schema: "Order",
                table: "OrderPricingAllocationSets",
                columns: new[] { "OrderPricingLineId", "Purpose", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderChanges",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPriceChangeSets",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPricingAllocations",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPricingAllocationSets",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OwnerAirlineId_Id",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_OrderId_SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_OriginalPricingLineId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropIndex(
                name: "IX_OrderPricingLines_PriceChangeSetId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "AncillaryTotal",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FinancialSequence",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OwnerAirlineId",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ApplicationLevel",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "BasisReferenceId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "CalculationSnapshot",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "OriginalAllocationId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "PriceChangeSetId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "RelatedOperationId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "SettlementCategory",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "SettlementPartyRef",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "SourceLineRef",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "TransferGroupId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.DropColumn(
                name: "TaxDetails",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "Order",
                table: "OrderPricingLines",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "SaleCurrencyId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineSubCategory",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "SaleAmount",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<decimal>(
                name: "EquivalentAmount",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.DropColumn(
                name: "OriginalCurrencyId",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineScope",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.DropColumn(
                name: "LineRole",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineReason",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "Effect",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineDirection",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "Direction",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "LineCategory",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "ComponentType",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "EquivalentCurrencyId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "BasisType",
                schema: "Order",
                table: "OrderPricingLines");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                schema: "Order",
                table: "OrderPricingLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPercentage",
                schema: "Order",
                table: "OrderPricingLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OrderPricingLineAllocations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    EquivalentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EquivalentCurrencyId = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    OrderPricingLineId = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalAllocationId = table.Column<long>(type: "bigint", nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetType = table.Column<int>(type: "int", nullable: true),
                    NumberOfDecimalPlaces = table.Column<int>(type: "int", nullable: true),
                    RateOfExchange = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    RateOfExchangeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoundingFactor = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingLineAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingLineAllocations_OrderPricingLines_OrderPricingLineId",
                        column: x => x.OrderPricingLineId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OrderId",
                schema: "Order",
                table: "OrderPricingLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLineAllocations_OrderPricingLineId",
                schema: "Order",
                table: "OrderPricingLineAllocations",
                column: "OrderPricingLineId");
        }
    }
}
