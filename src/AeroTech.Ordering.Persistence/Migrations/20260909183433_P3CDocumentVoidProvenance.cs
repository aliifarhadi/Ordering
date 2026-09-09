using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3CDocumentVoidProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VoidOperationId",
                schema: "Order",
                table: "ElectronicTickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidProviderReference",
                schema: "Order",
                table: "ElectronicTickets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidReason",
                schema: "Order",
                table: "ElectronicTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReasonDetail",
                schema: "Order",
                table: "ElectronicTickets",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                schema: "Order",
                table: "ElectronicTickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VoidedBy",
                schema: "Order",
                table: "ElectronicTickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VoidOperationId",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidProviderReference",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidReason",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReasonDetail",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VoidedBy",
                schema: "Order",
                table: "ElectronicMiscDocuments",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidOperationId",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidProviderReference",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidReasonDetail",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                schema: "Order",
                table: "ElectronicTickets");

            migrationBuilder.DropColumn(
                name: "VoidOperationId",
                schema: "Order",
                table: "ElectronicMiscDocuments");

            migrationBuilder.DropColumn(
                name: "VoidProviderReference",
                schema: "Order",
                table: "ElectronicMiscDocuments");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "Order",
                table: "ElectronicMiscDocuments");

            migrationBuilder.DropColumn(
                name: "VoidReasonDetail",
                schema: "Order",
                table: "ElectronicMiscDocuments");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                schema: "Order",
                table: "ElectronicMiscDocuments");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                schema: "Order",
                table: "ElectronicMiscDocuments");
        }
    }
}
