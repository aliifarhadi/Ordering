using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.ReferenceData.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ReferenceData");

            migrationBuilder.CreateTable(
                name: "Airlines",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IataCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airlines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Airports",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IataCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IkaoCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AirportTerminals",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AirportId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportTerminals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IataCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    RoundingFactor = table.Column<double>(type: "float", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UniqueIdentifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreferredCurrencyId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDataSyncStates",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastSync = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDataSyncStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Airlines_IataCode",
                schema: "ReferenceData",
                table: "Airlines",
                column: "IataCode");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_IataCode",
                schema: "ReferenceData",
                table: "Airports",
                column: "IataCode");

            migrationBuilder.CreateIndex(
                name: "IX_AirportTerminals_AirportId",
                schema: "ReferenceData",
                table: "AirportTerminals",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IataCode",
                schema: "ReferenceData",
                table: "Cities",
                column: "IataCode");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UniqueIdentifier",
                schema: "ReferenceData",
                table: "Customers",
                column: "UniqueIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Airlines",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "Airports",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "AirportTerminals",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "Cities",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "ReferenceData");

            migrationBuilder.DropTable(
                name: "ReferenceDataSyncStates",
                schema: "ReferenceData");
        }
    }
}
