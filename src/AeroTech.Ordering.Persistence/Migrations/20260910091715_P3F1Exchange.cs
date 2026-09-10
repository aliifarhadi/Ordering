using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3F1Exchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PredecessorTicketCouponId",
                schema: "Order",
                table: "TicketCoupons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PredecessorElectronicTicketId",
                schema: "Order",
                table: "ElectronicTickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PredecessorExchangeOperationId",
                schema: "Order",
                table: "ElectronicTickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlans",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    QuotedExchangeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetSelectionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePricingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PricingSource = table.Column<int>(type: "int", nullable: false),
                    SaleCurrencyId = table.Column<int>(type: "int", nullable: false),
                    PredecessorElectronicTicketId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PredecessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorCouponNumber = table.Column<int>(type: "int", nullable: false),
                    PredecessorOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorElectronicTicketId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedCommercialVersion = table.Column<int>(type: "int", nullable: false),
                    MonetaryOutcome = table.Column<int>(type: "int", nullable: false),
                    AcceptedPlan = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    DispositionDetail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RejectionCode = table.Column<int>(type: "int", nullable: true),
                    RejectionHttpStatus = table.Column<int>(type: "int", nullable: true),
                    EligibilityOutcome = table.Column<int>(type: "int", nullable: true),
                    EligibilityDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReservationOutcome = table.Column<int>(type: "int", nullable: false),
                    ReservationExternalRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DocumentExchangeOutcome = table.Column<int>(type: "int", nullable: true),
                    DocumentExchangeProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DocumentExchangeDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SuccessorDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SuccessorCouponNumber = table.Column<int>(type: "int", nullable: true),
                    SuccessorIssuerCarrierId = table.Column<long>(type: "bigint", nullable: true),
                    SuccessorIssuingOfficeId = table.Column<long>(type: "bigint", nullable: true),
                    SuccessorAuthority = table.Column<int>(type: "int", nullable: true),
                    SuccessorVoidDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlans", x => x.OperationId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentExchangeRecords",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorElectronicTicketId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorElectronicTicketId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    QuotedExchangeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetSelectionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePricingReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActorId = table.Column<long>(type: "bigint", nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExchangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentExchangeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentExchangeRecords_ElectronicTickets_PredecessorElectronicTicketId",
                        column: x => x.PredecessorElectronicTicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentExchangeCoupons",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DocumentExchangeRecordId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorCouponNumber = table.Column<int>(type: "int", nullable: false),
                    SuccessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorCouponNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentExchangeCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentExchangeCoupons_DocumentExchangeRecords_DocumentExchangeRecordId",
                        column: x => x.DocumentExchangeRecordId,
                        principalSchema: "Order",
                        principalTable: "DocumentExchangeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCoupons_PredecessorTicketCouponId",
                schema: "Order",
                table: "TicketCoupons",
                column: "PredecessorTicketCouponId",
                unique: true,
                filter: "[PredecessorTicketCouponId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicTickets_PredecessorElectronicTicketId",
                schema: "Order",
                table: "ElectronicTickets",
                column: "PredecessorElectronicTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlans_OrderId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlans_PredecessorElectronicTicketId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                column: "PredecessorElectronicTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlans_SuccessorElectronicTicketId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                column: "SuccessorElectronicTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeCoupons_DocumentExchangeRecordId_PredecessorTicketCouponId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                columns: new[] { "DocumentExchangeRecordId", "PredecessorTicketCouponId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeCoupons_PreviousOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                column: "PreviousOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeCoupons_ReplacementOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                column: "ReplacementOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeCoupons_SuccessorTicketCouponId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                column: "SuccessorTicketCouponId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeRecords_OperationId",
                schema: "Order",
                table: "DocumentExchangeRecords",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeRecords_PredecessorElectronicTicketId",
                schema: "Order",
                table: "DocumentExchangeRecords",
                column: "PredecessorElectronicTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExchangeRecords_SuccessorElectronicTicketId",
                schema: "Order",
                table: "DocumentExchangeRecords",
                column: "SuccessorElectronicTicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedExchangePlans",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentExchangeCoupons",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentExchangeRecords",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_TicketCoupons_PredecessorTicketCouponId",
                schema: "Order",
                table: "TicketCoupons");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicTickets_PredecessorElectronicTicketId",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "PredecessorTicketCouponId",
                schema: "Order",
                table: "TicketCoupons");

            migrationBuilder.DropColumn(
                name: "PredecessorElectronicTicketId",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "PredecessorExchangeOperationId",
                schema: "Order",
                table: "ElectronicTickets");
        }
    }
}
