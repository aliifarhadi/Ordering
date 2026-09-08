using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderViewItem(
        long OrderItemId,
        ProductType ProductType,
        string? ProductCode,
        string? ProductName,
        decimal Quantity,
        OrderItemUnitOfMeasure UnitOfMeasure,
        OrderItemCommercialStatus CommercialStatus,
        OrderViewProductSnapshot? ProductSnapshot,
        OrderViewCommercialTerms? CommercialTerms);

    public sealed record OrderViewProductSnapshot(
        ProductType ProductType,
        string SourceSystem,
        string SourceOfferId,
        string SourceProductReference,
        string? SourcePricingReference,
        string? ProductCode,
        string? ProductName,
        string? BrandCode,
        string? BrandName,
        int? MarketingAirlineId,
        int? OperatingAirlineId,
        string? SupplierCode,
        DateTimeOffset AcceptedAt);

    public sealed record OrderViewCommercialTerms(
        CommercialTermState RefundabilitySummary,
        CommercialTermState ChangeabilitySummary,
        CommercialTermState UpgradeEligibilitySummary,
        string SourceSystem,
        string? SourcePolicyReference,
        string? SourcePolicyVersion,
        DateTimeOffset TermsCapturedAt);

    public sealed record OrderViewService(
        long ServiceId,
        OrderServiceType ServiceType,
        string ServiceCode,
        string Name,
        OrderServiceStatus Status,
        OrderServiceCommercialStatus CommercialStatus,
        OrderFulfillmentStatus FulfillmentStatus,
        OrderServiceDeliveryStatus DeliveryStatus,
        OrderServiceFinancialStatus FinancialStatus,
        OrderServiceDocumentStatus DocumentStatus,
        ServicePriceTreatment PriceTreatment,
        long CurrentOrderItemId,
        IReadOnlyList<long> OriginalOrderItemMembership,
        IReadOnlyList<long> Beneficiaries,
        OrderViewServiceCoverage Coverage,
        OrderViewServiceFulfillment Fulfillment,
        OrderViewServiceDetail Detail,
        OrderViewEmdIssuance? EmdIssuance,
        long? ElectronicTicketId,
        long? TicketCouponId,
        long? ElectronicMiscDocumentId,
        long? EmdCouponId);

    public sealed record OrderViewServiceCoverage(
        IReadOnlyList<long> Services,
        IReadOnlyList<long> Segments);

    public sealed record OrderViewServiceFulfillment(
        DeliveryModel DeliveryModel,
        bool RequiresReservation,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        ServiceDocumentKind? DocumentKind,
        bool RequiresPaymentCoverage,
        OrderProviderType ProviderType,
        string? SupplierCode,
        string? HoldBatchId,
        string? SeatHoldReference);

    public sealed record OrderViewServiceDetail(
        string Kind,
        long? OrderSegmentId = null,
        string? TransitionalFareBasis = null,
        string? RequestedSeat = null,
        long? AssociatedAirOrderServiceId = null,
        string? SoldSeatNumber = null,
        BaggageServiceKind? BaggageKind = null,
        int? Pieces = null,
        decimal? Weight = null,
        BaggageWeightUnit? WeightUnit = null,
        decimal? PerPieceWeightLimit = null,
        string? MealCode = null,
        int? Quantity = null,
        string? SpecialMealCode = null,
        int? AirportId = null,
        string? LoungeCode = null,
        DateTimeOffset? AccessStart = null,
        DateTimeOffset? AccessEnd = null,
        int? GuestCount = null,
        long? RelatedAirOrderServiceId = null,
        string? PropertyReference = null,
        DateOnly? CheckIn = null,
        DateOnly? CheckOut = null,
        int? RoomCount = null,
        string? RoomTypeCode = null,
        string? PickupLocationReference = null,
        string? DropoffLocationReference = null,
        DateTimeOffset? PickupAt = null,
        int? PassengerCount = null,
        string? VehicleTypeCode = null,
        string? SchemaName = null,
        string? SchemaVersion = null);

    public sealed record OrderViewEmdIssuance(
        ElectronicMiscDocumentType EmdType,
        string ReasonForIssuanceCode,
        string ReasonForIssuanceSubCode,
        long? AssociatedAirOrderServiceId,
        string? SourceSystem,
        string? SourceReference,
        DateTimeOffset CapturedAt);
}
