using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3G2AncillaryRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundApprovedAmount",
                schema: "Order",
                table: "EmdCoupons",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundApprovedDisposition",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundCurrencyId",
                schema: "Order",
                table: "EmdCoupons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDecisionReference",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundOperationId",
                schema: "Order",
                table: "EmdCoupons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundProviderReference",
                schema: "Order",
                table: "EmdCoupons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAt",
                schema: "Order",
                table: "EmdCoupons",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundCurrencyId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDisposition",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDocumentDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundDocumentOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDocumentReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundValueDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundValueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundValueReference",
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
                name: "RefundApprovedAmount",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundApprovedDisposition",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundCurrencyId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundDecisionReference",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundOperationId",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundProviderReference",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                schema: "Order",
                table: "EmdCoupons");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundCurrencyId",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundDisposition",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundDocumentDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundDocumentOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundDocumentReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundSourceReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundValueDetail",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundValueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");

            migrationBuilder.DropColumn(
                name: "RefundValueReference",
                schema: "Order",
                table: "AcceptedExchangePlanAncillaries");
        }
    }
}
