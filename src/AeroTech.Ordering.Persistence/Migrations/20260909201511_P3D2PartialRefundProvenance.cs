using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3D2PartialRefundProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualAuthorityReference",
                schema: "Order",
                table: "DocumentRefundRecords",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualRefundReason",
                schema: "Order",
                table: "DocumentRefundRecords",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingSource",
                schema: "Order",
                table: "DocumentRefundRecords",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "SourceEvidence",
                schema: "Order",
                table: "DocumentRefundRecords",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRefundType",
                schema: "Order",
                table: "DocumentRefundRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualAuthorityReference",
                schema: "Order",
                table: "DocumentRefundRecords");

            migrationBuilder.DropColumn(
                name: "ManualRefundReason",
                schema: "Order",
                table: "DocumentRefundRecords");

            migrationBuilder.DropColumn(
                name: "PricingSource",
                schema: "Order",
                table: "DocumentRefundRecords");

            migrationBuilder.DropColumn(
                name: "SourceEvidence",
                schema: "Order",
                table: "DocumentRefundRecords");

            migrationBuilder.DropColumn(
                name: "SourceRefundType",
                schema: "Order",
                table: "DocumentRefundRecords");
        }
    }
}
