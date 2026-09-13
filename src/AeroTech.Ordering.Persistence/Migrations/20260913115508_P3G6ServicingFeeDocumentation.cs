using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G6ServicingFeeDocumentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlanFeeDocuments",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IssuerCarrierId = table.Column<long>(type: "bigint", nullable: false),
                    TravelerId = table.Column<long>(type: "bigint", nullable: true),
                    ReasonForIssuanceCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Coupons = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    AllocatedDocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IssuanceOutcome = table.Column<int>(type: "int", nullable: true),
                    IssuanceProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IssuanceDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ElectronicMiscDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlanFeeDocuments", x => new { x.OperationId, x.DocumentReference });
                    table.ForeignKey(
                        name: "FK_AcceptedExchangePlanFeeDocuments_AcceptedExchangePlans_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Order",
                        principalTable: "AcceptedExchangePlans",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanFeeDocuments_AllocatedDocumentNumber",
                schema: "Order",
                table: "AcceptedExchangePlanFeeDocuments",
                column: "AllocatedDocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanFeeDocuments_ElectronicMiscDocumentId",
                schema: "Order",
                table: "AcceptedExchangePlanFeeDocuments",
                column: "ElectronicMiscDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedExchangePlanFeeDocuments",
                schema: "Order");
        }
    }
}
