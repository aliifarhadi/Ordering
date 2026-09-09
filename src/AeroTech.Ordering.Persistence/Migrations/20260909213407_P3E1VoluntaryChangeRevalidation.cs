using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3E1VoluntaryChangeRevalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcceptedChangePlans",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    QuotedChangeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetSelectionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ElectronicTicketId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacedOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    TicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedCommercialVersion = table.Column<int>(type: "int", nullable: false),
                    MonetaryOutcome = table.Column<int>(type: "int", nullable: false),
                    AcceptedPlan = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReservationOutcome = table.Column<int>(type: "int", nullable: false),
                    ReservationExternalRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedChangePlans", x => x.OperationId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRevalidationRecords",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    QuotedChangeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetSelectionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    NewOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RevalidatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RevalidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRevalidationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRevalidationRecords_ElectronicTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "Order",
                        principalTable: "ElectronicTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedChangePlans_OrderId",
                schema: "Order",
                table: "AcceptedChangePlans",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevalidationRecords_TicketCouponId",
                schema: "Order",
                table: "DocumentRevalidationRecords",
                column: "TicketCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevalidationRecords_TicketId_OperationId",
                schema: "Order",
                table: "DocumentRevalidationRecords",
                columns: new[] { "TicketId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedChangePlans",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "DocumentRevalidationRecords",
                schema: "Order");
        }
    }
}
