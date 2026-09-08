using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2B1DomainSemanticDecoupling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CabinBaggagePieces",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "CabinBaggageUnit",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "CabinBaggageWeight",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "CheckedBaggagePieces",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "CheckedBaggageUnit",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "CheckedBaggageWeight",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "IsChangeable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "IsRefundable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "IsUpgradable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.RenameColumn(
                name: "Brand",
                schema: "Order",
                table: "OrderItemProductSnapshots",
                newName: "BrandName");

            migrationBuilder.RenameColumn(
                name: "SourceRuleReference",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                newName: "SourcePolicyReference");

            migrationBuilder.RenameColumn(
                name: "PolicySource",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                newName: "SourceSystem");

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                schema: "Order",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                schema: "Order",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "BrandCode",
                schema: "Order",
                table: "OrderItemProductSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChangeabilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RefundabilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SourcePolicyVersion",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpgradeEligibilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandCode",
                schema: "Order",
                table: "OrderItemProductSnapshots");

            migrationBuilder.DropColumn(
                name: "ChangeabilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "RefundabilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "SourcePolicyVersion",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.DropColumn(
                name: "UpgradeEligibilitySummary",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots");

            migrationBuilder.RenameColumn(
                name: "BrandName",
                schema: "Order",
                table: "OrderItemProductSnapshots",
                newName: "Brand");

            migrationBuilder.RenameColumn(
                name: "SourceSystem",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                newName: "PolicySource");

            migrationBuilder.RenameColumn(
                name: "SourcePolicyReference",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                newName: "SourceRuleReference");

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                schema: "Order",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                schema: "Order",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CabinBaggagePieces",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CabinBaggageUnit",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CabinBaggageWeight",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckedBaggagePieces",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckedBaggageUnit",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckedBaggageWeight",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChangeable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefundable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUpgradable",
                schema: "Order",
                table: "OrderItemCommercialTermsSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
