using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3FNegativeBalanceExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundDueDetail",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundDueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundDueReference",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidualDetail",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidualInstrument",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidualInstrumentReference",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidualOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidualProviderReference",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundDueDetail",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "RefundDueOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "RefundDueReference",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ResidualDetail",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ResidualInstrument",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ResidualInstrumentReference",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ResidualOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "ResidualProviderReference",
                schema: "Order",
                table: "AcceptedExchangePlans");
        }
    }
}
