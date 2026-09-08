using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2DServiceComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequiresFulfillment",
                schema: "Order",
                table: "OrderServices",
                newName: "RequiresReservation");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryProviderReference",
                schema: "Order",
                table: "OrderServices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentKind",
                schema: "Order",
                table: "OrderServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceTreatment",
                schema: "Order",
                table: "OrderServices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPaymentCoverage",
                schema: "Order",
                table: "OrderServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OrderAirTransportServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    TransitionalFareBasis = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestedSeat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TransitionalCheckedBaggageWeight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TransitionalCheckedBaggageUnit = table.Column<int>(type: "int", nullable: true),
                    TransitionalCheckedBaggagePieces = table.Column<int>(type: "int", nullable: true),
                    TransitionalCabinBaggageWeight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TransitionalCabinBaggageUnit = table.Column<int>(type: "int", nullable: true),
                    TransitionalCabinBaggagePieces = table.Column<int>(type: "int", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAirTransportServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAirTransportServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderBaggageServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WeightUnit = table.Column<int>(type: "int", nullable: true),
                    PerPieceWeightLimit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBaggageServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderBaggageServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderGenericServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AttributesJson = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderGenericServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderGenericServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderGroundTransportServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    PickupLocationReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DropoffLocationReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PickupAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PassengerCount = table.Column<int>(type: "int", nullable: false),
                    VehicleTypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderGroundTransportServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderGroundTransportServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderHotelServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    PropertyReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    RoomCount = table.Column<int>(type: "int", nullable: false),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    SupplierReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RoomTypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RatePlanReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderHotelServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderHotelServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemServiceLinks",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedByChangeId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemServiceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemServiceLinks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLoungeServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    AirportId = table.Column<int>(type: "int", nullable: false),
                    LoungeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AccessStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AccessEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    RelatedAirOrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLoungeServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLoungeServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderMealServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    MealCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SpecialMealCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderMealServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderMealServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderSeatServiceDetails",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    AssociatedAirOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    SoldSeatNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSeatServiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSeatServiceDetails_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServiceBeneficiaries",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    OrderTravellerId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServiceBeneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServiceBeneficiaries_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServiceCoveredSegments",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServiceCoveredSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServiceCoveredSegments_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServiceCoveredServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    CoveredOrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServiceCoveredServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServiceCoveredServices_OrderServices_OrderServiceId",
                        column: x => x.OrderServiceId,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderServices_OrderItemId",
                schema: "Order",
                table: "OrderServices",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirTransportServiceDetails_OrderSegmentId",
                schema: "Order",
                table: "OrderAirTransportServiceDetails",
                column: "OrderSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAirTransportServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderAirTransportServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderBaggageServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderBaggageServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderGenericServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderGenericServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderGroundTransportServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderGroundTransportServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderHotelServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderHotelServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemServiceLinks_OrderId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemServiceLinks_OrderItemId_OrderServiceId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                columns: new[] { "OrderItemId", "OrderServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemServiceLinks_OrderServiceId",
                schema: "Order",
                table: "OrderItemServiceLinks",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLoungeServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderLoungeServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderMealServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderMealServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSeatServiceDetails_OrderServiceId",
                schema: "Order",
                table: "OrderSeatServiceDetails",
                column: "OrderServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceBeneficiaries_OrderServiceId_OrderTravellerId",
                schema: "Order",
                table: "OrderServiceBeneficiaries",
                columns: new[] { "OrderServiceId", "OrderTravellerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceBeneficiaries_OrderTravellerId",
                schema: "Order",
                table: "OrderServiceBeneficiaries",
                column: "OrderTravellerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceCoveredSegments_OrderServiceId_OrderSegmentId",
                schema: "Order",
                table: "OrderServiceCoveredSegments",
                columns: new[] { "OrderServiceId", "OrderSegmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceCoveredServices_CoveredOrderServiceId",
                schema: "Order",
                table: "OrderServiceCoveredServices",
                column: "CoveredOrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceCoveredServices_OrderServiceId_CoveredOrderServiceId",
                schema: "Order",
                table: "OrderServiceCoveredServices",
                columns: new[] { "OrderServiceId", "CoveredOrderServiceId" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [Order].[OrderAirTransportServiceDetails]
                    ([Id],[OrderServiceId],[OrderSegmentId],[TransitionalFareBasis],[RequestedSeat],
                     [TransitionalCheckedBaggageWeight],[TransitionalCheckedBaggageUnit],[TransitionalCheckedBaggagePieces],
                     [TransitionalCabinBaggageWeight],[TransitionalCabinBaggageUnit],[TransitionalCabinBaggagePieces],
                     [LastUpdateTime])
                SELECT ROW_NUMBER() OVER (ORDER BY a.[Id]), a.[Id], a.[OrderSegmentId], a.[FareBasis], a.[Seat],
                       a.[Baggage_Weight], a.[Baggage_Unit], a.[Baggage_Pieces],
                       a.[CabinBaggage_Weight], a.[CabinBaggage_Unit], a.[CabinBaggage_Pieces],
                       SYSDATETIMEOFFSET()
                FROM [Order].[OrderAirTransportServices] a;
                """);

            migrationBuilder.Sql("""
                INSERT INTO [Order].[OrderServiceBeneficiaries]
                    ([Id],[OrderServiceId],[OrderTravellerId],[LastUpdateTime])
                SELECT 1000000 + ROW_NUMBER() OVER (ORDER BY a.[Id]), a.[Id], a.[TravellerId], SYSDATETIMEOFFSET()
                FROM [Order].[OrderAirTransportServices] a;
                """);

            migrationBuilder.Sql("""
                INSERT INTO [Order].[OrderItemServiceLinks]
                    ([Id],[OrderId],[OrderItemId],[OrderServiceId],[LinkedByChangeId],[LinkedAt],[LastUpdateTime])
                SELECT 2000000 + ROW_NUMBER() OVER (ORDER BY s.[Id]), s.[OrderId], s.[OrderItemId], s.[Id],
                       (SELECT TOP 1 c.[Id] FROM [Order].[OrderChanges] c
                        WHERE c.[OrderId] = s.[OrderId] ORDER BY c.[OccurredAt], c.[Id]),
                       s.[CreatedAt], SYSDATETIMEOFFSET()
                FROM [Order].[OrderServices] s
                WHERE EXISTS (SELECT 1 FROM [Order].[OrderChanges] c WHERE c.[OrderId] = s.[OrderId]);
                """);

            migrationBuilder.DropTable(
                name: "OrderAirTransportServices",
                schema: "Order");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderAirTransportServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderBaggageServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderGenericServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderGroundTransportServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderHotelServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderItemServiceLinks",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderLoungeServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderMealServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderSeatServiceDetails",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderServiceBeneficiaries",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderServiceCoveredSegments",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderServiceCoveredServices",
                schema: "Order");

            migrationBuilder.DropIndex(
                name: "IX_OrderServices_OrderItemId",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "DeliveryProviderReference",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "DocumentKind",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "PriceTreatment",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.DropColumn(
                name: "RequiresPaymentCoverage",
                schema: "Order",
                table: "OrderServices");

            migrationBuilder.RenameColumn(
                name: "RequiresReservation",
                schema: "Order",
                table: "OrderServices",
                newName: "RequiresFulfillment");

            migrationBuilder.CreateTable(
                name: "OrderAirTransportServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AirFareId = table.Column<long>(type: "bigint", nullable: true),
                    FareBasis = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FareFamilyTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FareNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsChangeable = table.Column<bool>(type: "bit", nullable: false),
                    IsRefundable = table.Column<bool>(type: "bit", nullable: false),
                    IsUpgradable = table.Column<bool>(type: "bit", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    Seat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TravellerId = table.Column<long>(type: "bigint", nullable: false),
                    Baggage_Pieces = table.Column<int>(type: "int", nullable: true),
                    Baggage_Unit = table.Column<int>(type: "int", nullable: true),
                    Baggage_Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CabinBaggage_Pieces = table.Column<int>(type: "int", nullable: true),
                    CabinBaggage_Unit = table.Column<int>(type: "int", nullable: true),
                    CabinBaggage_Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAirTransportServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAirTransportServices_OrderServices_Id",
                        column: x => x.Id,
                        principalSchema: "Order",
                        principalTable: "OrderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
