using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G5AncillaryCancelAndManualReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationDocumentAction",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CancelledOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualReviewReason",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlanAncillaryCancelGroups",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    CancelGroupRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    EmdDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmdCouponNumbers = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrderServiceIds = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CancellationReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentAction = table.Column<int>(type: "int", nullable: false),
                    DecisionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VoidEligibilityOutcome = table.Column<int>(type: "int", nullable: true),
                    VoidRefundRequiredInstead = table.Column<bool>(type: "bit", nullable: false),
                    VoidEligibilityDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    VoidDispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidOutcome = table.Column<int>(type: "int", nullable: true),
                    VoidProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VoidDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CancellationSettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlanAncillaryCancelGroups", x => new { x.OperationId, x.CancelGroupRef });
                    table.ForeignKey(
                        name: "FK_AcceptedExchangePlanAncillaryCancelGroups_AcceptedExchangePlans_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Order",
                        principalTable: "AcceptedExchangePlans",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaries_OperationId_CancelGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                columns: new[] { "OperationId", "CancelGroupRef" });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanAncillaryCancelGroups_ElectronicMiscDocumentId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaryCancelGroups",
                column: "ElectronicMiscDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedExchangePlanAncillaryCancelGroups",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_AcceptedExchangePlanAncillaries_OperationId_CancelGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "CancelGroupRef",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "CancellationDocumentAction",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "CancellationReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "CancellationSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "CancelledOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "ManualReviewReason",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
