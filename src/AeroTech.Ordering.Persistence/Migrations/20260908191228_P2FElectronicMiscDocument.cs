using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2FElectronicMiscDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ElectronicMiscDocumentId",
                schema: "Order",
                table: "OrderServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EmdCouponId",
                schema: "Order",
                table: "OrderServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectronicMiscDocuments",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OriginalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentServicingOrderId = table.Column<long>(type: "bigint", nullable: false),
                    TravelerId = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ReasonForIssuanceCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IssuerCarrierId = table.Column<long>(type: "bigint", nullable: false),
                    IssuingOfficeId = table.Column<long>(type: "bigint", nullable: true),
                    Authority = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StatusSummary = table.Column<int>(type: "int", nullable: false),
                    DocumentVersion = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicMiscDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderServiceEmdIssuanceSnapshots",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    EmdType = table.Column<int>(type: "int", nullable: false),
                    ReasonForIssuanceCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ReasonForIssuanceSubCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    AssociatedAirOrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentGroupReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServiceEmdIssuanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServiceEmdIssuanceSnapshots_OrderServices_AssociatedAirOrderServiceId",
                        column: x => x.AssociatedAirOrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderServiceEmdIssuanceSnapshots_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmdCoupons",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    ReasonForIssuanceSubCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    PricingLineId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalValueReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssociatedTicketCouponId = table.Column<long>(type: "bigint", nullable: true),
                    IssuanceValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmdCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmdCoupons_ElectronicMiscDocuments_ElectronicMiscDocumentId",
                        column: x => x.ElectronicMiscDocumentId,
                        principalSchema: "Order",
                        principalTable: "ElectronicMiscDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmdCoupons_OrderPricingLines_PricingLineId",
                        column: x => x.PricingLineId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmdCoupons_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmdCoupons_TicketCoupons_AssociatedTicketCouponId",
                        column: x => x.AssociatedTicketCouponId,
                        principalSchema: "Order",
                        principalTable: "TicketCoupons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmdPriceLinks",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    EmdCouponId = table.Column<long>(type: "bigint", nullable: false),
                    PricingLineId = table.Column<long>(type: "bigint", nullable: false),
                    AllocationId = table.Column<long>(type: "bigint", nullable: true),
                    AttributedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmdPriceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmdPriceLinks_ElectronicMiscDocuments_ElectronicMiscDocumentId",
                        column: x => x.ElectronicMiscDocumentId,
                        principalSchema: "Order",
                        principalTable: "ElectronicMiscDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmdPriceLinks_EmdCoupons_EmdCouponId",
                        column: x => x.EmdCouponId,
                        principalSchema: "Order",
                        principalTable: "EmdCoupons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmdPriceLinks_OrderPricingAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingAllocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmdPriceLinks_OrderPricingLines_PricingLineId",
                        column: x => x.PricingLineId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingLines",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicMiscDocuments_CurrentServicingOrderId",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                column: "CurrentServicingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicMiscDocuments_DocumentNumber",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicMiscDocuments_OperationId",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicMiscDocuments_OriginalOrderId",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                column: "OriginalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdCoupons_AssociatedTicketCouponId",
                schema: "Order",
                table: "EmdCoupons",
                column: "AssociatedTicketCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdCoupons_ElectronicMiscDocumentId_CouponNumber",
                schema: "Order",
                table: "EmdCoupons",
                columns: new[] { "ElectronicMiscDocumentId", "CouponNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmdCoupons_OrderServiceId",
                schema: "Order",
                table: "EmdCoupons",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdCoupons_PricingLineId",
                schema: "Order",
                table: "EmdCoupons",
                column: "PricingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdPriceLinks_AllocationId",
                schema: "Order",
                table: "EmdPriceLinks",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdPriceLinks_ElectronicMiscDocumentId_EmdCouponId",
                schema: "Order",
                table: "EmdPriceLinks",
                columns: new[] { "ElectronicMiscDocumentId", "EmdCouponId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmdPriceLinks_EmdCouponId",
                schema: "Order",
                table: "EmdPriceLinks",
                column: "EmdCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_EmdPriceLinks_PricingLineId",
                schema: "Order",
                table: "EmdPriceLinks",
                column: "PricingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceEmdIssuanceSnapshots_AssociatedAirOrderServiceId",
                schema: "Order",
                table: "OrderServiceEmdIssuanceSnapshots",
                column: "AssociatedAirOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceEmdIssuanceSnapshots_OrderServiceId",
                schema: "Order",
                table: "OrderServiceEmdIssuanceSnapshots",
                column: "OrderServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmdPriceLinks",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderServiceEmdIssuanceSnapshots",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "EmdCoupons",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "ElectronicMiscDocuments",
                schema: "Order");

            migrationBuilder.DropColumn(
                name: "ElectronicMiscDocumentId",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "EmdCouponId",
                schema: "Order",
                table: "OrderServices");
        }
    }
}
