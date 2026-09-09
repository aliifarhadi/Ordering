using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3D1DocumentRefundHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentRefundRecords",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    QuotedRefundId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePricingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    ApprovedDisposition = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DispositionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RefundedBy = table.Column<long>(type: "bigint", nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PriceChangeSetId = table.Column<long>(type: "bigint", nullable: true),
                    ValueMovementStatus = table.Column<int>(type: "int", nullable: false),
                    ValueMovementReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ValueMovementDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ValueMovementUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRefundRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRefundRecords_ElectronicTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRefundCoupons",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DocumentRefundRecordId = table.Column<long>(type: "bigint", nullable: false),
                    TicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRefundCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRefundCoupons_DocumentRefundRecords_DocumentRefundRecordId",
                        column: x => x.DocumentRefundRecordId,
                        principalSchema: "Order",
                        principalTable: "DocumentRefundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCoupons_DocumentRefundRecordId_TicketCouponId",
                schema: "Order",
                table: "DocumentRefundCoupons",
                columns: new[] { "DocumentRefundRecordId", "TicketCouponId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundCoupons_OrderServiceId",
                schema: "Order",
                table: "DocumentRefundCoupons",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundRecords_OperationId",
                schema: "Order",
                table: "DocumentRefundRecords",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRefundRecords_TicketId_OperationId",
                schema: "Order",
                table: "DocumentRefundRecords",
                columns: new[] { "TicketId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRefundCoupons",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentRefundRecords",
                schema: "Order");
        }
    }
}
