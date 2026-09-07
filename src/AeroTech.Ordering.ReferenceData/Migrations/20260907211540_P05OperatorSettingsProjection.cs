using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.ReferenceData.Migrations
{
    /// <inheritdoc />
    public partial class P05OperatorSettingsProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorSettings",
                schema: "ReferenceData",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HomeAirlineId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSettings_ScopeKey",
                schema: "ReferenceData",
                table: "OperatorSettings",
                column: "ScopeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorSettings",
                schema: "ReferenceData");
        }
    }
}
