using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1OrderVerticalSlice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ElectronicTicketId",
                schema: "Order",
                table: "OrderServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TicketCouponId",
                schema: "Order",
                table: "OrderServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "Order",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommercialSummary",
                schema: "Order",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ObligationVersion",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ParentOrderId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RootOrderId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SplitFromChangeId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentStocks",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OwnerAirlineId = table.Column<long>(type: "bigint", nullable: false),
                    AirlineOfficeId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SerialWidth = table.Column<int>(type: "int", nullable: false),
                    CheckDigitProfile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RangeFrom = table.Column<long>(type: "bigint", nullable: false),
                    RangeTo = table.Column<long>(type: "bigint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentStocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicTickets",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OriginalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentServicingOrderId = table.Column<long>(type: "bigint", nullable: false),
                    TravelerId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IssuerCarrierId = table.Column<int>(type: "int", nullable: false),
                    IssuingOfficeId = table.Column<long>(type: "bigint", nullable: true),
                    Authority = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VoidDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IssuedTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    StatusSummary = table.Column<int>(type: "int", nullable: false),
                    DocumentVersion = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentReservations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExternalReservationRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderExternalReferences",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExternalReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderExternalReferences_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderTimeLimits",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTimeLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTimeLimits_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentStockAllocations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DocumentStockId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Serial = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AllocatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentStockAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentStockAllocations_DocumentStocks_DocumentStockId",
                        column: x => x.DocumentStockId,
                        principalSchema: "Order",
                        principalTable: "DocumentStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentPriceLinks",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    CouponId = table.Column<long>(type: "bigint", nullable: true),
                    PricingLineId = table.Column<long>(type: "bigint", nullable: false),
                    AllocationId = table.Column<long>(type: "bigint", nullable: true),
                    AttributedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentPriceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentPriceLinks_ElectronicTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketCoupons",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    JourneySegmentId = table.Column<long>(type: "bigint", nullable: false),
                    IssuedMarketingAirlineId = table.Column<int>(type: "int", nullable: false),
                    IssuedFlightNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IssuedOriginAirportId = table.Column<int>(type: "int", nullable: false),
                    IssuedDestinationAirportId = table.Column<int>(type: "int", nullable: false),
                    IssuedDepartureDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedArrivalDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedBookingClass = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    FareBasisSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IssuanceValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    FinancialStatus = table.Column<int>(type: "int", nullable: false),
                    ControlStatus = table.Column<int>(type: "int", nullable: false),
                    ProviderCouponStatusCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCoupons_ElectronicTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentReservationServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentReservationId = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalServiceRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ObservedStatus = table.Column<int>(type: "int", nullable: false),
                    ObservedBookingClass = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ExternalStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentReservationServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FulfillmentReservationServices_FulfillmentReservations_FulfillmentReservationId",
                        column: x => x.FulfillmentReservationId,
                        principalSchema: "Order",
                        principalTable: "FulfillmentReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CommercialSummary",
                schema: "Order",
                table: "Orders",
                column: "CommercialSummary");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RootOrderId",
                schema: "Order",
                table: "Orders",
                column: "RootOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentPriceLinks_PricingLineId",
                schema: "Order",
                table: "DocumentPriceLinks",
                column: "PricingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentPriceLinks_TicketId_CouponId",
                schema: "Order",
                table: "DocumentPriceLinks",
                columns: new[] { "TicketId", "CouponId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStockAllocations_DocumentNumber",
                schema: "Order",
                table: "DocumentStockAllocations",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStockAllocations_DocumentStockId_Serial",
                schema: "Order",
                table: "DocumentStockAllocations",
                columns: new[] { "DocumentStockId", "Serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStockAllocations_OperationId_DocumentRole",
                schema: "Order",
                table: "DocumentStockAllocations",
                columns: new[] { "OperationId", "DocumentRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStocks_OwnerAirlineId_DocumentType_Status",
                schema: "Order",
                table: "DocumentStocks",
                columns: new[] { "OwnerAirlineId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_CurrentServicingOrderId",
                schema: "Order",
                table: "ElectronicTickets",
                column: "CurrentServicingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_DocumentNumber",
                schema: "Order",
                table: "ElectronicTickets",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_OperationId_TravelerId",
                schema: "Order",
                table: "ElectronicTickets",
                columns: new[] { "OperationId", "TravelerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_OriginalOrderId",
                schema: "Order",
                table: "ElectronicTickets",
                column: "OriginalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentReservations_OperationId",
                schema: "Order",
                table: "FulfillmentReservations",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentReservations_OrderId",
                schema: "Order",
                table: "FulfillmentReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentReservationServices_FulfillmentReservationId_OrderServiceId",
                schema: "Order",
                table: "FulfillmentReservationServices",
                columns: new[] { "FulfillmentReservationId", "OrderServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentReservationServices_OrderServiceId",
                schema: "Order",
                table: "FulfillmentReservationServices",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderExternalReferences_OrderId_Type",
                schema: "Order",
                table: "OrderExternalReferences",
                columns: new[] { "OrderId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderExternalReferences_SourceSystem_Reference",
                schema: "Order",
                table: "OrderExternalReferences",
                columns: new[] { "SourceSystem", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTimeLimits_OrderId_Type_Status",
                schema: "Order",
                table: "OrderTimeLimits",
                columns: new[] { "OrderId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTimeLimits_Status_DueAt",
                schema: "Order",
                table: "OrderTimeLimits",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCoupons_CurrentOrderServiceId",
                schema: "Order",
                table: "TicketCoupons",
                column: "CurrentOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCoupons_TicketId_CouponNumber",
                schema: "Order",
                table: "TicketCoupons",
                columns: new[] { "TicketId", "CouponNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentPriceLinks",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentStockAllocations",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "FulfillmentReservationServices",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderExternalReferences",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderTimeLimits",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "TicketCoupons",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentStocks",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "FulfillmentReservations",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "ElectronicTickets",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CommercialSummary",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RootOrderId",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ElectronicTicketId",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "TicketCouponId",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CommercialSummary",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ObligationVersion",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ParentOrderId",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RootOrderId",
                schema: "Order",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SplitFromChangeId",
                schema: "Order",
                table: "Orders");
        }
    }
}
