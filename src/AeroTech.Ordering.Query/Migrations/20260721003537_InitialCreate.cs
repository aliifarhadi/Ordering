using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Query.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ReadModel");

            migrationBuilder.CreateTable(
                name: "OrderFlights",
                schema: "ReadModel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FlightNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarketingAirlineId = table.Column<int>(type: "int", nullable: false),
                    OriginAirportId = table.Column<int>(type: "int", nullable: false),
                    DestinationAirportId = table.Column<int>(type: "int", nullable: false),
                    DepartureDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArrivalDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFlights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "ReadModel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UniqueIdentifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordLocator = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    OfficeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Pax = table.Column<int>(type: "int", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderVersion = table.Column<int>(type: "int", nullable: false),
                    LinkedOrderId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedPNR = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimeToLive = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastProjectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderTravellers",
                schema: "ReadModel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SurName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AgeRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTravellers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderFlights_DepartureDateTime",
                schema: "ReadModel",
                table: "OrderFlights",
                column: "DepartureDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFlights_FlightNumber",
                schema: "ReadModel",
                table: "OrderFlights",
                column: "FlightNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFlights_OrderId",
                schema: "ReadModel",
                table: "OrderFlights",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreationDate",
                schema: "ReadModel",
                table: "Orders",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                schema: "ReadModel",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                schema: "ReadModel",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTravellers_FirstName",
                schema: "ReadModel",
                table: "OrderTravellers",
                column: "FirstName");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTravellers_OrderId",
                schema: "ReadModel",
                table: "OrderTravellers",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTravellers_SurName",
                schema: "ReadModel",
                table: "OrderTravellers",
                column: "SurName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderFlights",
                schema: "ReadModel");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "ReadModel");

            migrationBuilder.DropTable(
                name: "OrderTravellers",
                schema: "ReadModel");
        }
    }
}
