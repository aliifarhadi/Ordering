using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3D3RefundCorrectionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentRefundCorrectionRecords",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentRefundRecordId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalRefundOperationId = table.Column<long>(type: "bigint", nullable: false),
                    CorrectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReasonDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrectedBy = table.Column<long>(type: "bigint", nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PriceChangeSetId = table.Column<long>(type: "bigint", nullable: true),
                    ValueCorrectionStatus = table.Column<int>(type: "int", nullable: false),
                    ValueCorrectionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ValueCorrectionDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ValueCorrectionUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRefundCorrectionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRefundCorrectionRecords_ElectronicTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRefundCorrectionCoupons",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DocumentRefundCorrectionRecordId = table.Column<long>(type: "bigint", nullable: false),
                    TicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRefundCorrectionCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRefundCorrectionCoupons_DocumentRefundCorrectionRecords_DocumentRefundCorrectionRecordId",
                        column: x => x.DocumentRefundCorrectionRecordId,
                        principalSchema: "Order",
                        principalTable: "DocumentRefundCorrectionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCorrectionCoupons_DocumentRefundCorrectionRecordId_TicketCouponId",
                schema: "Order",
                table: "DocumentRefundCorrectionCoupons",
                columns: new[] { "DocumentRefundCorrectionRecordId", "TicketCouponId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCorrectionCoupons_OrderServiceId",
                schema: "Order",
                table: "DocumentRefundCorrectionCoupons",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCorrectionRecords_DocumentRefundRecordId",
                schema: "Order",
                table: "DocumentRefundCorrectionRecords",
                column: "DocumentRefundRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCorrectionRecords_OperationId",
                schema: "Order",
                table: "DocumentRefundCorrectionRecords",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCorrectionRecords_TicketId_OperationId",
                schema: "Order",
                table: "DocumentRefundCorrectionRecords",
                columns: new[] { "TicketId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRefundCorrectionCoupons",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentRefundCorrectionRecords",
                schema: "Order");
        }
    }
}
