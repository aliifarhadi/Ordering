using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G1EmdAssociationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlanAncillaries",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    EmdCouponId = table.Column<long>(type: "bigint", nullable: false),
                    ElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    EmdDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmdCouponNumber = table.Column<int>(type: "int", nullable: false),
                    PredecessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PredecessorCouponNumber = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    TargetPredecessorCouponNumber = table.Column<int>(type: "int", nullable: true),
                    TargetSuccessorTicketCouponId = table.Column<long>(type: "bigint", nullable: true),
                    DecisionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DecisionVersion = table.Column<int>(type: "int", nullable: false),
                    AssociationOutcome = table.Column<int>(type: "int", nullable: true),
                    AssociationProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssociationDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlanAncillaries", x => new { x.OperationId, x.EmdCouponId });
                    table.ForeignKey(
                        name: "FK_AcceptedExchangePlanAncillaries_AcceptedExchangePlans_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Order",
                        principalTable: "AcceptedExchangePlans",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmdCouponAssociationChanges",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    EmdCouponId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PreviousTicketCouponId = table.Column<long>(type: "bigint", nullable: true),
                    PreviousDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PreviousCouponNumber = table.Column<int>(type: "int", nullable: true),
                    CurrentTicketCouponId = table.Column<long>(type: "bigint", nullable: true),
                    CurrentDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CurrentCouponNumber = table.Column<int>(type: "int", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    DecisionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmdCouponAssociationChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmdCouponAssociationChanges_EmdCoupons_EmdCouponId",
                        column: x => x.EmdCouponId,
                        principalSchema: "Order",
                        principalTable: "EmdCoupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmdCouponAssociationChanges_EmdCouponId_Sequence",
                schema: "Order",
                table: "EmdCouponAssociationChanges",
                columns: new[] { "EmdCouponId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedExchangePlanAncillaries",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "EmdCouponAssociationChanges",
                schema: "Order");
        }
    }
}
