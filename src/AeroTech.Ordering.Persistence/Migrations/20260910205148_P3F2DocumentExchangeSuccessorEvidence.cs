using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3F2DocumentExchangeSuccessorEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentExchangeSuccessorEvidence",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(max)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentExchangeSuccessorEvidence",
                schema: "Order",
                table: "AcceptedExchangePlans");
        }
    }
}
