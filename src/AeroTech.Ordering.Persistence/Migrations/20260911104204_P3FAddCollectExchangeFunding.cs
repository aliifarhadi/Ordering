using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3FAddCollectExchangeFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FundingCaptureDetail",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FundingCaptureOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingCaptureReference",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingGuaranteeDetail",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FundingGuaranteeOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingGuaranteeReference",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingMethodRef",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingReleaseDetail",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FundingReleaseOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FundingCaptureDetail",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingCaptureOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingCaptureReference",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingGuaranteeDetail",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingGuaranteeOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingGuaranteeReference",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingMethodRef",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingReleaseDetail",
                schema: "Order",
                table: "AcceptedExchangePlans");

            migrationBuilder.DropColumn(
                name: "FundingReleaseOutcome",
                schema: "Order",
                table: "AcceptedExchangePlans");
        }
    }
}
