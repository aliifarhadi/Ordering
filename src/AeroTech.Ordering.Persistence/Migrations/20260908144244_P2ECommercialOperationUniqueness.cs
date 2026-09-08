using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2ECommercialOperationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @duplicates int;
                SELECT @duplicates = COUNT(*) FROM (
                    SELECT [OrderId], [OperationId]
                    FROM [Order].[OrderChanges]
                    WHERE [OperationId] IS NOT NULL
                    GROUP BY [OrderId], [OperationId]
                    HAVING COUNT(*) > 1) duplicated;
                IF @duplicates > 0
                    THROW 62001, 'P2-E commercial operation uniqueness: OrderChanges already holds more than one commercial mutation for the same (OrderId, OperationId). Historical data must be corrected explicitly - this migration does not delete or repoint commercial history.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderChanges_OrderId_OperationId",
                schema: "Order",
                table: "OrderChanges",
                columns: new[] { "OrderId", "OperationId" },
                unique: true,
                filter: "[OperationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderChanges_OrderId_OperationId",
                schema: "Order",
                table: "OrderChanges");
        }
    }
}
