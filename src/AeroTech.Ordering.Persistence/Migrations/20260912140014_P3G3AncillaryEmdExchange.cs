using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G3AncillaryEmdExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExchangeDecisionReference",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExchangeOperationId",
                schema: "Order",
                table: "EmdCoupons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeProviderReference",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeSuccessorCouponNumber",
                schema: "Order",
                table: "EmdCoupons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeSuccessorDocumentNumber",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExchangeSuccessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExchangedAt",
                schema: "Order",
                table: "EmdCoupons",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredecessorCouponNumber",
                schema: "Order",
                table: "EmdCoupons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PredecessorDocumentNumber",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PredecessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlanAncillaryExchangeGroups",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    ExchangeGroupRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    SourceDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceCouponNumbers = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SuccessorType = table.Column<int>(type: "int", nullable: false),
                    SuccessorReasonForIssuanceCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    SuccessorCoupons = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    DecisionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PricingSource = table.Column<int>(type: "int", nullable: false),
                    PricingLines = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    AddCollectAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AddCollectCurrencyId = table.Column<int>(type: "int", nullable: true),
                    RefundDueAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundDueCurrencyId = table.Column<int>(type: "int", nullable: true),
                    RefundDueDisposition = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResidualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ResidualCurrencyId = table.Column<int>(type: "int", nullable: true),
                    ResidualDisposition = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResidualExpectedInstrument = table.Column<int>(type: "int", nullable: true),
                    ResidualFulfillment = table.Column<int>(type: "int", nullable: true),
                    FundingMethodRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExchangeOutcome = table.Column<int>(type: "int", nullable: true),
                    ExchangeProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExchangeDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SuccessorElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    SuccessorDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PriceChangeSetId = table.Column<long>(type: "bigint", nullable: true),
                    FundingGuaranteeOutcome = table.Column<int>(type: "int", nullable: true),
                    FundingGuaranteeReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FundingGuaranteeDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FundingCaptureOutcome = table.Column<int>(type: "int", nullable: true),
                    FundingCaptureReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FundingCaptureDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ResidualOutcome = table.Column<int>(type: "int", nullable: true),
                    ResidualProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResidualInstrumentReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResidualInstrument = table.Column<int>(type: "int", nullable: true),
                    ResidualDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlanAncillaryExchangeGroups", x => new { x.OperationId, x.ExchangeGroupRef });
                    table.ForeignKey(
                        name: "FK_AcceptedExchangePlanAncillaryExchangeGroups_AcceptedExchangePlans_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Order",
                        principalTable: "AcceptedExchangePlans",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmdCoupons_PredecessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons",
                column: "PredecessorElectronicMiscDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaries_OperationId_ExchangeGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                columns: new[] { "OperationId", "ExchangeGroupRef" });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaryExchangeGroups_SourceElectronicMiscDocumentId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                column: "SourceElectronicMiscDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaryExchangeGroups_SuccessorDocumentNumber",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                column: "SuccessorDocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaryExchangeGroups_SuccessorElectronicMiscDocumentId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryExchangeGroups",
                column: "SuccessorElectronicMiscDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedExchangePlanAncillaryExchangeGroups",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_EmdCoupons_PredecessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropIndex(
                name: "IX_AcceptedExchangePlanAncillaries_OperationId_ExchangeGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "ExchangeDecisionReference",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeOperationId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeProviderReference",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeSuccessorCouponNumber",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeSuccessorDocumentNumber",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeSuccessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangedAt",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "PredecessorCouponNumber",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "PredecessorDocumentNumber",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "PredecessorElectronicMiscDocumentId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "ExchangeGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
