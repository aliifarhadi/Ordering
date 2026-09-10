using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3F2ExchangeTicketedSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SegmentArrivalDateTime",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SegmentBookingClass",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SegmentDepartureDateTime",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "SegmentDestinationAirportId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SegmentFlightNumber",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SegmentMarketingAirlineId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SegmentOriginAirportId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegmentArrivalDateTime",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentBookingClass",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentDepartureDateTime",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentDestinationAirportId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentFlightNumber",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentMarketingAirlineId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");

            migrationBuilder.DropColumn(
                name: "SegmentOriginAirportId",
                schema: "Order",
                table: "AcceptedExchangePlanCoupons");
        }
    }
}
