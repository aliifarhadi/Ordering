using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G4AncillaryRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionMode",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetentionSettledAt",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionMode",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RetentionReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RetentionSettledAt",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RetentionSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
