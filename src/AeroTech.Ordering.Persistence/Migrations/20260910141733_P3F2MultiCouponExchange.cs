using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3F2MultiCouponExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PredecessorTravellerId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE accepted
                SET accepted.[PredecessorTravellerId] = ticket.[TravelerId]
                FROM [Order].[AcceptedExchangePlans] accepted
                INNER JOIN [Order].[ElectronicTickets] ticket ON ticket.[Id] = accepted.[PredecessorElectronicTicketId];
                """);

            migrationBuilder.CreateTable(
                name: "AcceptedExchangePlanCoupons",
                schema: "Order",
                columns: table => new
                {
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorCouponNumber = table.Column<int>(type: "int", nullable: false),
                    PredecessorOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    SuccessorTicketCouponId = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementOrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    ReplacementOrderSegmentId = table.Column<long>(type: "bigint", nullable: true),
                    SuccessorCouponNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedExchangePlanCoupons", x => new { x.OperationId, x.PredecessorTicketCouponId });
                    table.ForeignKey(
                        name: "FK_AcceptedExchangePlanCoupons_AcceptedExchangePlans_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Order",
                        principalTable: "AcceptedExchangePlans",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanCoupons_PredecessorOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                column: "PredecessorOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedExchangePlanCoupons_SuccessorTicketCouponId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                column: "SuccessorTicketCouponId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [Order].[AcceptedExchangePlanCoupons]
                    ([OperationId], [PredecessorTicketCouponId], [PredecessorCouponNumber], [PredecessorOrderServiceId],
                     [Disposition], [SuccessorTicketCouponId], [ReplacementOrderServiceId], [ReplacementOrderSegmentId], [SuccessorCouponNumber])
                SELECT
                    accepted.[OperationId], accepted.[PredecessorTicketCouponId], accepted.[PredecessorCouponNumber], accepted.[PredecessorOrderServiceId],
                    1, accepted.[SuccessorTicketCouponId], accepted.[ReplacementOrderServiceId], accepted.[ReplacementOrderSegmentId], accepted.[SuccessorCouponNumber]
                FROM [Order].[AcceptedExchangePlans] accepted;
                """);

            migrationBuilder.DropColumn(
                name: "PredecessorCouponNumber",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "PredecessorOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "PredecessorTicketCouponId",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ReplacementOrderSegmentId",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ReplacementOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "SuccessorCouponNumber",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "SuccessorTicketCouponId",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.RenameColumn(
                name: "ReplacementOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                newName: "SuccessorOrderServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentExchangeCoupons_ReplacementOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                newName: "IX_DocumentExchangeCoupons_SuccessorOrderServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SuccessorOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                newName: "ReplacementOrderServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentExchangeCoupons_SuccessorOrderServiceId",
                schema: "Order",
                table: "DocumentExchangeCoupons",
                newName: "IX_DocumentExchangeCoupons_ReplacementOrderServiceId");

            migrationBuilder.AddColumn<int>(
                name: "PredecessorCouponNumber",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PredecessorOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PredecessorTicketCouponId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ReplacementOrderSegmentId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ReplacementOrderServiceId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "SuccessorCouponNumber",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SuccessorTicketCouponId",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE accepted
                SET accepted.[PredecessorCouponNumber] = coupon.[PredecessorCouponNumber],
                    accepted.[PredecessorOrderServiceId] = coupon.[PredecessorOrderServiceId],
                    accepted.[PredecessorTicketCouponId] = coupon.[PredecessorTicketCouponId],
                    accepted.[ReplacementOrderSegmentId] = ISNULL(coupon.[ReplacementOrderSegmentId], 0),
                    accepted.[ReplacementOrderServiceId] = ISNULL(coupon.[ReplacementOrderServiceId], 0),
                    accepted.[SuccessorCouponNumber] = coupon.[SuccessorCouponNumber],
                    accepted.[SuccessorTicketCouponId] = coupon.[SuccessorTicketCouponId]
                FROM [Order].[AcceptedExchangePlans] accepted
                INNER JOIN [Order].[AcceptedExchangePlanCoupons] coupon
                    ON coupon.[OperationId] = accepted.[OperationId]
                   AND coupon.[PredecessorCouponNumber] = (
                        SELECT MIN(first.[PredecessorCouponNumber])
                        FROM [Order].[AcceptedExchangePlanCoupons] first
                        WHERE first.[OperationId] = accepted.[OperationId] AND first.[Disposition] = 1);
                """);

            migrationBuilder.DropTable(
                name: "AcceptedExchangePlanCoupons",
                schema: "Order");

            migrationBuilder.DropColumn(
                name: "PredecessorTravellerId",
                schema: "Order",
                table: "AcceptedExchangePlans");
        }
    }
}
