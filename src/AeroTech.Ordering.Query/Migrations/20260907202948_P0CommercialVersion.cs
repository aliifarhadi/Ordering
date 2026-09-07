using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Query.Migrations
{
    /// <inheritdoc />
    public partial class P0CommercialVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderVersion",
                schema: "ReadModel",
                table: "Orders",
                newName: "CommercialVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommercialVersion",
                schema: "ReadModel",
                table: "Orders",
                newName: "OrderVersion");
        }
    }
}
