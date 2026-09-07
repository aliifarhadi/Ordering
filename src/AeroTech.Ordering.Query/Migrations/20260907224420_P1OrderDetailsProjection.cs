using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Query.Migrations
{
    /// <inheritdoc />
    public partial class P1OrderDetailsProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommercialSummary",
                schema: "ReadModel",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CoverageSummary",
                schema: "ReadModel",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentSummary",
                schema: "ReadModel",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectionRevision",
                schema: "ReadModel",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ReservationSummary",
                schema: "ReadModel",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderDetails",
                schema: "ReadModel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionRevision = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetails", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDetails",
                schema: "ReadModel");

            migrationBuilder.DropColumn(
                name: "CommercialSummary",
                schema: "ReadModel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CoverageSummary",
                schema: "ReadModel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DocumentSummary",
                schema: "ReadModel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProjectionRevision",
                schema: "ReadModel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReservationSummary",
                schema: "ReadModel",
                table: "Orders");
        }
    }
}
