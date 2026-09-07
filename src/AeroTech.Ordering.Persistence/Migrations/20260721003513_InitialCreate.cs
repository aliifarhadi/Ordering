using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroTech.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Document");

            migrationBuilder.EnsureSchema(
                name: "Fulfillment");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "Order");

            migrationBuilder.EnsureSchema(
                name: "Payment");

            migrationBuilder.EnsureSchema(
                name: "Provider");

            migrationBuilder.CreateTable(
                name: "FulfillmentTasks",
                schema: "Fulfillment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<int>(type: "int", nullable: true),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    CanRunInParallel = table.Column<bool>(type: "bit", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReceivedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RecordLocator = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    UniqueIdentifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderVersion = table.Column<int>(type: "int", nullable: false),
                    LinkedOrderId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedPNR = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Pax = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    OfficeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeToLive = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReservationFailureReason = table.Column<int>(type: "int", nullable: true),
                    CommissionRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseFareTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SurchargeTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PenaltyTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "Payment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    FormOfPayment = table.Column<int>(type: "int", nullable: false),
                    WalletReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<int>(type: "int", nullable: true),
                    VoidedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderInteractions",
                schema: "Provider",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentTaskId = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentTaskAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrafficDocuments",
                schema: "Document",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    TravellerId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentUniqueCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuerSystem = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VoidDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<int>(type: "int", nullable: true),
                    VoidReasonDetail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    VoidedBy = table.Column<long>(type: "bigint", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelReason = table.Column<int>(type: "int", nullable: true),
                    CancelledBy = table.Column<long>(type: "bigint", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IssueChannel = table.Column<int>(type: "int", nullable: false),
                    IssueUserId = table.Column<long>(type: "bigint", nullable: true),
                    IssueReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Fare = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxesTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeesTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmdType = table.Column<int>(type: "int", nullable: true),
                    ReasonForIssuanceCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentTaskAttempts",
                schema: "Fulfillment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentTaskId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    ProviderInteractionId = table.Column<long>(type: "bigint", nullable: false),
                    IsRetriable = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentTaskAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FulfillmentTaskAttempts_FulfillmentTasks_FulfillmentTaskId",
                        column: x => x.FulfillmentTaskId,
                        principalSchema: "Fulfillment",
                        principalTable: "FulfillmentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentTaskTargets",
                schema: "Fulfillment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentTaskId = table.Column<long>(type: "bigint", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FulfillmentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServiceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentTaskTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FulfillmentTaskTargets_FulfillmentTasks_FulfillmentTaskId",
                        column: x => x.FulfillmentTaskId,
                        principalSchema: "Fulfillment",
                        principalTable: "FulfillmentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderContacts",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderContacts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductType = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitOfMeasure = table.Column<int>(type: "int", nullable: false),
                    CommercialStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    FulfillmentStatus = table.Column<int>(type: "int", nullable: false),
                    FinancialStatus = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItineraries",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OriginAirportId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationAirportId = table.Column<long>(type: "bigint", nullable: false),
                    BoundId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    BoundDirection = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItineraries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItineraries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPaymentSummaries",
                schema: "Order",
                columns: table => new
                {
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CapturedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FormOfPayment = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPaymentSummaries", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_OrderPaymentSummaries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingLines",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    LineReason = table.Column<int>(type: "int", nullable: false),
                    LineScope = table.Column<int>(type: "int", nullable: false),
                    LineCategory = table.Column<int>(type: "int", nullable: false),
                    LineSubCategory = table.Column<int>(type: "int", nullable: false),
                    LineDirection = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    IsPercentage = table.Column<bool>(type: "bit", nullable: false),
                    EquivalentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EquivalentCurrencyId = table.Column<int>(type: "int", nullable: false),
                    RateOfExchange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NumberOfDecimalPlaces = table.Column<int>(type: "int", nullable: true),
                    RateOfExchangeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoundingFactor = table.Column<int>(type: "int", nullable: true),
                    Refundability = table.Column<int>(type: "int", nullable: false),
                    OriginalPricingLineId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderRemarks",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    TravellerId = table.Column<long>(type: "bigint", nullable: true),
                    SegmentId = table.Column<long>(type: "bigint", nullable: true),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPrintedOnItinerary = table.Column<bool>(type: "bit", nullable: false),
                    IsPrintedOnInvoice = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderRemarks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderSegments",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItineraryId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    CabinClassId = table.Column<int>(type: "int", nullable: true),
                    RbdId = table.Column<long>(type: "bigint", nullable: true),
                    BookingClassCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FlightCapacityId = table.Column<long>(type: "bigint", nullable: false),
                    BookingClass = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    AirFareId = table.Column<long>(type: "bigint", nullable: false),
                    FlightId = table.Column<long>(type: "bigint", nullable: false),
                    FlightVersion = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OriginAirportId = table.Column<int>(type: "int", nullable: false),
                    OriginAirportTerminalId = table.Column<int>(type: "int", nullable: true),
                    DestinationAirportId = table.Column<int>(type: "int", nullable: false),
                    DestinationAirportTerminalId = table.Column<int>(type: "int", nullable: true),
                    OperatingAirlineId = table.Column<int>(type: "int", nullable: false),
                    MarketingAirlineId = table.Column<int>(type: "int", nullable: false),
                    DepartureDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArrivalDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    AircraftId = table.Column<int>(type: "int", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSegments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CommercialStatus = table.Column<int>(type: "int", nullable: false),
                    FulfillmentStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                    FinancialStatus = table.Column<int>(type: "int", nullable: false),
                    DocumentStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveryModel = table.Column<int>(type: "int", nullable: false),
                    RequiresFulfillment = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSupplierConfirmation = table.Column<bool>(type: "bit", nullable: false),
                    RequiresDocument = table.Column<bool>(type: "bit", nullable: false),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HoldBatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SeatHoldReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrafficDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentCouponId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderTravellers",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SurName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NoSurname = table.Column<bool>(type: "bit", nullable: false),
                    PassengerType = table.Column<int>(type: "int", nullable: false),
                    AgeRange = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    NationalityId = table.Column<int>(type: "int", nullable: false),
                    CountryOfResidenceId = table.Column<int>(type: "int", nullable: false),
                    ParentTravellerId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTravellers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTravellers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Order",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCoupons",
                schema: "Document",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TrafficDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: false),
                    CouponNumber = table.Column<int>(type: "int", nullable: false),
                    CouponUniqueCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Fare = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxesTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeesTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    CouponType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AssociatedTicketCouponId = table.Column<long>(type: "bigint", nullable: true),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentCoupons_TrafficDocuments_TrafficDocumentId",
                        column: x => x.TrafficDocumentId,
                        principalSchema: "Document",
                        principalTable: "TrafficDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderContactPoints",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderContactId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderContactPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderContactPoints_OrderContacts_OrderContactId",
                        column: x => x.OrderContactId,
                        principalSchema: "Order",
                        principalTable: "OrderContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemPolicySnapshots",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    DeliveryModel = table.Column<int>(type: "int", nullable: false),
                    AccountingGranularity = table.Column<int>(type: "int", nullable: false),
                    AssignmentMode = table.Column<int>(type: "int", nullable: false),
                    RequiresPassenger = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSegment = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSupplierConfirmation = table.Column<bool>(type: "bit", nullable: false),
                    RequiresDocument = table.Column<bool>(type: "bit", nullable: false),
                    RequiresFulfillment = table.Column<bool>(type: "bit", nullable: false),
                    CanBeUnassignedAtPurchase = table.Column<bool>(type: "bit", nullable: false),
                    CanBeTransferred = table.Column<bool>(type: "bit", nullable: false),
                    CanBePartiallyConsumed = table.Column<bool>(type: "bit", nullable: false),
                    RefundRuleRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangeRuleRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CancellationRuleRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupplierPolicyRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SnapshotAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SnapshotVersion = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemPolicySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemPolicySnapshots_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "Order",
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingLineAllocations",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderPricingLineId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    OrderServiceId = table.Column<long>(type: "bigint", nullable: true),
                    TargetType = table.Column<int>(type: "int", nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    EquivalentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EquivalentCurrencyId = table.Column<int>(type: "int", nullable: false),
                    RateOfExchange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NumberOfDecimalPlaces = table.Column<int>(type: "int", nullable: true),
                    RateOfExchangeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoundingFactor = table.Column<int>(type: "int", nullable: true),
                    OriginalAllocationId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingLineAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingLineAllocations_OrderPricingLines_OrderPricingLineId",
                        column: x => x.OrderPricingLineId,
                        principalSchema: "Order",
                        principalTable: "OrderPricingLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderSegmentLegs",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    LegId = table.Column<long>(type: "bigint", nullable: false),
                    OriginAirportId = table.Column<int>(type: "int", nullable: false),
                    OriginAirportTerminalId = table.Column<int>(type: "int", nullable: true),
                    DestinationAirportId = table.Column<int>(type: "int", nullable: false),
                    DestinationAirportTerminalId = table.Column<int>(type: "int", nullable: true),
                    DepartureDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArrivalDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StopType = table.Column<int>(type: "int", nullable: true),
                    StopDurationAtArrivalAirport = table.Column<int>(type: "int", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSegmentLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSegmentLegs_OrderSegments_OrderSegmentId",
                        column: x => x.OrderSegmentId,
                        principalSchema: "Order",
                        principalTable: "OrderSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderAirTransportServices",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    TravellerId = table.Column<long>(type: "bigint", nullable: false),
                    Seat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    AirFareId = table.Column<long>(type: "bigint", nullable: true),
                    FareBasis = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FareFamilyTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FareNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsChangeable = table.Column<bool>(type: "bit", nullable: false),
                    IsRefundable = table.Column<bool>(type: "bit", nullable: false),
                    IsUpgradable = table.Column<bool>(type: "bit", nullable: false),
                    Baggage_Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Baggage_Unit = table.Column<int>(type: "int", nullable: true),
                    Baggage_Pieces = table.Column<int>(type: "int", nullable: true),
                    CabinBaggage_Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CabinBaggage_Unit = table.Column<int>(type: "int", nullable: true),
                    CabinBaggage_Pieces = table.Column<int>(type: "int", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "OrderTravellerDocuments",
                schema: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuanceCountryId = table.Column<int>(type: "int", nullable: false),
                    Holder = table.Column<bool>(type: "bit", nullable: false),
                    OrderTravellerId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTravellerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTravellerDocuments_OrderTravellers_OrderTravellerId",
                        column: x => x.OrderTravellerId,
                        principalSchema: "Order",
                        principalTable: "OrderTravellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCoupons_OrderServiceId",
                schema: "Document",
                table: "DocumentCoupons",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCoupons_TrafficDocumentId",
                schema: "Document",
                table: "DocumentCoupons",
                column: "TrafficDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTaskAttempts_FulfillmentTaskId",
                schema: "Fulfillment",
                table: "FulfillmentTaskAttempts",
                column: "FulfillmentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTaskAttempts_ProviderInteractionId",
                schema: "Fulfillment",
                table: "FulfillmentTaskAttempts",
                column: "ProviderInteractionId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTasks_OrderId",
                schema: "Fulfillment",
                table: "FulfillmentTasks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTasks_Status_NextRetryAt",
                schema: "Fulfillment",
                table: "FulfillmentTasks",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTaskTargets_FulfillmentTaskId",
                schema: "Fulfillment",
                table: "FulfillmentTaskTargets",
                column: "FulfillmentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentTaskTargets_OrderServiceId",
                schema: "Fulfillment",
                table: "FulfillmentTaskTargets",
                column: "OrderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ReceivedOn",
                schema: "dbo",
                table: "InboxMessages",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_OrderContactPoints_OrderContactId",
                schema: "Order",
                table: "OrderContactPoints",
                column: "OrderContactId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderContacts_OrderId",
                schema: "Order",
                table: "OrderContacts",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemPolicySnapshots_OrderItemId",
                schema: "Order",
                table: "OrderItemPolicySnapshots",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                schema: "Order",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItineraries_OrderId",
                schema: "Order",
                table: "OrderItineraries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLineAllocations_OrderPricingLineId",
                schema: "Order",
                table: "OrderPricingLineAllocations",
                column: "OrderPricingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLines_OrderId",
                schema: "Order",
                table: "OrderPricingLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderRemarks_OrderId_Status",
                schema: "Order",
                table: "OrderRemarks",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderRemarks_OrderId_Type",
                schema: "Order",
                table: "OrderRemarks",
                columns: new[] { "OrderId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RecordLocator",
                schema: "Order",
                table: "Orders",
                column: "RecordLocator",
                unique: true,
                filter: "[RecordLocator] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_TimeToLive",
                schema: "Order",
                table: "Orders",
                columns: new[] { "Status", "TimeToLive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSegmentLegs_OrderSegmentId",
                schema: "Order",
                table: "OrderSegmentLegs",
                column: "OrderSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSegments_OrderId",
                schema: "Order",
                table: "OrderSegments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServices_OrderId",
                schema: "Order",
                table: "OrderServices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTravellerDocuments_OrderTravellerId",
                schema: "Order",
                table: "OrderTravellerDocuments",
                column: "OrderTravellerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTravellers_OrderId",
                schema: "Order",
                table: "OrderTravellers",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOn",
                schema: "dbo",
                table: "OutboxMessages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                schema: "Payment",
                table: "Payments",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                schema: "Payment",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInteractions_FulfillmentTaskId",
                schema: "Provider",
                table: "ProviderInteractions",
                column: "FulfillmentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInteractions_IdempotencyKey",
                schema: "Provider",
                table: "ProviderInteractions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_TrafficDocuments_DocumentNumber",
                schema: "Document",
                table: "TrafficDocuments",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrafficDocuments_OrderId",
                schema: "Document",
                table: "TrafficDocuments",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCoupons",
                schema: "Document");

            migrationBuilder.DropTable(
                name: "FulfillmentTaskAttempts",
                schema: "Fulfillment");

            migrationBuilder.DropTable(
                name: "FulfillmentTaskTargets",
                schema: "Fulfillment");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrderAirTransportServices",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderContactPoints",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderItemPolicySnapshots",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderItineraries",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPaymentSummaries",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPricingLineAllocations",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderRemarks",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderSegmentLegs",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderTravellerDocuments",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "Payment");

            migrationBuilder.DropTable(
                name: "ProviderInteractions",
                schema: "Provider");

            migrationBuilder.DropTable(
                name: "TrafficDocuments",
                schema: "Document");

            migrationBuilder.DropTable(
                name: "FulfillmentTasks",
                schema: "Fulfillment");

            migrationBuilder.DropTable(
                name: "OrderServices",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderContacts",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderPricingLines",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderSegments",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "OrderTravellers",
                schema: "Order");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "Order");
        }
    }
}
