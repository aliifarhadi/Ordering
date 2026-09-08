using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2D1ServiceTreatmentAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P2D1ServicePriceTreatmentBackfill.Sql);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderServices] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderItems] i WHERE i.[Id] = s.[OrderItemId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderServices.OrderItemId -> OrderItems.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderServiceBeneficiaries] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderTravellers] i WHERE i.[Id] = s.[OrderTravellerId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderServiceBeneficiaries.OrderTravellerId -> OrderTravellers.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderServiceCoveredSegments] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderSegments] i WHERE i.[Id] = s.[OrderSegmentId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderServiceCoveredSegments.OrderSegmentId -> OrderSegments.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderServiceCoveredServices] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderServices] i WHERE i.[Id] = s.[CoveredOrderServiceId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderServiceCoveredServices.CoveredOrderServiceId -> OrderServices.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderItemServiceLinks] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderItems] i WHERE i.[Id] = s.[OrderItemId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderItemServiceLinks.OrderItemId -> OrderItems.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderItemServiceLinks] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderServices] i WHERE i.[Id] = s.[OrderServiceId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderItemServiceLinks.OrderServiceId -> OrderServices.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderItemServiceLinks] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderChanges] i WHERE i.[Id] = s.[LinkedByChangeId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderItemServiceLinks.LinkedByChangeId -> OrderChanges.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderAirTransportServiceDetails] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderSegments] i WHERE i.[Id] = s.[OrderSegmentId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderAirTransportServiceDetails.OrderSegmentId -> OrderSegments.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderSeatServiceDetails] s
                WHERE NOT EXISTS (SELECT 1 FROM [Order].[OrderServices] i WHERE i.[Id] = s.[AssociatedAirOrderServiceId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderSeatServiceDetails.AssociatedAirOrderServiceId -> OrderServices.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @orphans int;
                SELECT @orphans = COUNT(*) FROM [Order].[OrderLoungeServiceDetails] s
                WHERE s.[RelatedAirOrderServiceId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Order].[OrderServices] i WHERE i.[Id] = s.[RelatedAirOrderServiceId]);
                IF @orphans > 0
                    THROW 62000, 'P2-D.1 referential integrity: OrderLoungeServiceDetails.RelatedAirOrderServiceId -> OrderServices.Id has rows whose target does not exist. Historical data must be corrected explicitly - this migration does not delete, repoint or invent referenced rows.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceCoveredSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderServiceCoveredSegments",
                column: "OrderSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSeatServiceDetails_AssociatedAirOrderServiceId",
                schema: "Order",
                table: "OrderSeatServiceDetails",
                column: "AssociatedAirOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLoungeServiceDetails_RelatedAirOrderServiceId",
                schema: "Order",
                table: "OrderLoungeServiceDetails",
                column: "RelatedAirOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemServiceLinks_LinkedByChangeId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "LinkedByChangeId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderAirTransportServiceDetails_OrderSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderAirTransportServiceDetails",
                column: "OrderSegmentId",
                principalSchema: "Order",
                principalTable: "OrderSegments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItemServiceLinks_OrderChanges_LinkedByChangeId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "LinkedByChangeId",
                principalSchema: "Order",
                principalTable: "OrderChanges",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItemServiceLinks_OrderItems_OrderItemId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "OrderItemId",
                principalSchema: "Order",
                principalTable: "OrderItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItemServiceLinks_OrderServices_OrderServiceId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "OrderServiceId",
                principalSchema: "Order",
                principalTable: "OrderServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLoungeServiceDetails_OrderServices_RelatedAirOrderServiceId",
                schema: "Order",
                table: "OrderLoungeServiceDetails",
                column: "RelatedAirOrderServiceId",
                principalSchema: "Order",
                principalTable: "OrderServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderSeatServiceDetails_OrderServices_AssociatedAirOrderServiceId",
                schema: "Order",
                table: "OrderSeatServiceDetails",
                column: "AssociatedAirOrderServiceId",
                principalSchema: "Order",
                principalTable: "OrderServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderServiceBeneficiaries_OrderTravellers_OrderTravellerId",
                schema: "Order",
                table: "OrderServiceBeneficiaries",
                column: "OrderTravellerId",
                principalSchema: "Order",
                principalTable: "OrderTravellers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderServiceCoveredSegments_OrderSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderServiceCoveredSegments",
                column: "OrderSegmentId",
                principalSchema: "Order",
                principalTable: "OrderSegments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderServiceCoveredServices_OrderServices_CoveredOrderServiceId",
                schema: "Order",
                table: "OrderServiceCoveredServices",
                column: "CoveredOrderServiceId",
                principalSchema: "Order",
                principalTable: "OrderServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderServices_OrderItems_OrderItemId",
                schema: "Order",
                table: "OrderServices",
                column: "OrderItemId",
                principalSchema: "Order",
                principalTable: "OrderItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderAirTransportServiceDetails_OrderSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderAirTransportServiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItemServiceLinks_OrderChanges_LinkedByChangeId",
                schema: "Order",
                table: "OrderItemServiceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItemServiceLinks_OrderItems_OrderItemId",
                schema: "Order",
                table: "OrderItemServiceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItemServiceLinks_OrderServices_OrderServiceId",
                schema: "Order",
                table: "OrderItemServiceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLoungeServiceDetails_OrderServices_RelatedAirOrderServiceId",
                schema: "Order",
                table: "OrderLoungeServiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderSeatServiceDetails_OrderServices_AssociatedAirOrderServiceId",
                schema: "Order",
                table: "OrderSeatServiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderServiceBeneficiaries_OrderTravellers_OrderTravellerId",
                schema: "Order",
                table: "OrderServiceBeneficiaries");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderServiceCoveredSegments_OrderSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderServiceCoveredSegments");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderServiceCoveredServices_OrderServices_CoveredOrderServiceId",
                schema: "Order",
                table: "OrderServiceCoveredServices");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderServices_OrderItems_OrderItemId",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropIndex(
                name: "IX_OrderServiceCoveredSegments_OrderSegmentId",
                schema: "Order",
                table: "OrderServiceCoveredSegments");

            migrationBuilder.DropIndex(
                name: "IX_OrderSeatServiceDetails_AssociatedAirOrderServiceId",
                schema: "Order",
                table: "OrderSeatServiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_OrderLoungeServiceDetails_RelatedAirOrderServiceId",
                schema: "Order",
                table: "OrderLoungeServiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_OrderItemServiceLinks_LinkedByChangeId",
                schema: "Order",
                table: "OrderItemServiceLinks");
        }
    }
}
