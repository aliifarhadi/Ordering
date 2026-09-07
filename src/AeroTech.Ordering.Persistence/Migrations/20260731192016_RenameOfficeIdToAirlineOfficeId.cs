using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOfficeIdToAirlineOfficeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OfficeId",
                schema: "Order",
                table: "Orders",
                newName: "AirlineOfficeId");

            migrationBuilder.AlterColumn<long>(
                name: "AirlineOfficeId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "AirlineOfficeId",
                schema: "Order",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: false);

            migrationBuilder.RenameColumn(
                name: "AirlineOfficeId",
                schema: "Order",
                table: "Orders",
                newName: "OfficeId");
        }
    }
}
