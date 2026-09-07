using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P0OperationsAndRatePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderVersion",
                schema: "Order",
                table: "Orders",
                newName: "CommercialVersion");

            migrationBuilder.AlterColumn<decimal>(
                name: "RateOfExchange",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RateOfExchange",
                schema: "Order",
                table: "OrderPricingLineAllocations",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CommandReceipts",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OwnerAirlineId = table.Column<long>(type: "bigint", nullable: false),
                    CallerScope = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationOrderClaims",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecoveryLeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationOrderClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServicingOperations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OwnerAirlineId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CommandReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpectedCommercialVersion = table.Column<int>(type: "int", nullable: true),
                    QuoteRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClaimGeneration = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicingOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandReceipts_OrderId",
                schema: "Order",
                table: "CommandReceipts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandReceipts_OwnerAirlineId_CallerScope_OperationName_IdempotencyKey",
                schema: "Order",
                table: "CommandReceipts",
                columns: new[] { "OwnerAirlineId", "CallerScope", "OperationName", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationOrderClaims_OperationId",
                schema: "Order",
                table: "OperationOrderClaims",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationOrderClaims_OrderId",
                schema: "Order",
                table: "OperationOrderClaims",
                column: "OrderId",
                unique: true,
                filter: "[IsBlocking] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ServicingOperations_CommandReceiptId",
                schema: "Order",
                table: "ServicingOperations",
                column: "CommandReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicingOperations_OrderId_Status",
                schema: "Order",
                table: "ServicingOperations",
                columns: new[] { "OrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandReceipts",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OperationOrderClaims",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "ServicingOperations",
                schema: "Order");

            migrationBuilder.RenameColumn(
                name: "CommercialVersion",
                schema: "Order",
                table: "Orders",
                newName: "OrderVersion");

            migrationBuilder.AlterColumn<decimal>(
                name: "RateOfExchange",
                schema: "Order",
                table: "OrderPricingLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,12)",
                oldPrecision: 28,
                oldScale: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RateOfExchange",
                schema: "Order",
                table: "OrderPricingLineAllocations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,12)",
                oldPrecision: 28,
                oldScale: 12,
                oldNullable: true);
        }
    }
}
