using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderPricingChanged(

        long OrderId,
        long OwnerAirlineId,

        long OrderChangeId,
        long? OperationId,

        long PriceChangeSetId,
        long FinancialSequence,

        int CommercialVersion,
        int EventOrdinal,
        long ObligationVersion,

        PriceChangeReason Reason,
        PricingSource PricingSource,

        string? SourceOfferId,
        string? SourcePricingReference,

        DateTimeOffset CommittedAt,

        int CurrencyId,
        decimal DerivedCustomerBalanceImpact,
        decimal CustomerTotalAfter,

        IReadOnlyList<OrderPricingChangedLine> PricingLines) : BaseIntegrationEvent;

    public record OrderPricingChangedLine(

        long PricingLineId,

        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,

        decimal OriginalAmount,
        int OriginalCurrencyId,

        decimal SaleAmount,
        int SaleCurrencyId,

        OrderPricingChangedExchangeRate? ExchangeRate,

        RefundabilityRule Refundability,

        PricingBasisType BasisType,
        long? BasisReferenceId,
        long? OrderItemId,

        PricingApplicationLevel? ApplicationLevel,
        decimal? Quantity,
        string? UnitOfMeasure,
        decimal? UnitPrice,

        string? Code,
        string? Description,

        string? SourceLineRef,
        string? OccurrenceKey,

        long? OriginalPricingLineId,
        long? OriginalAllocationId,
        long? RelatedOperationId,

        string? SettlementPartyRef,
        string? SettlementCategory,

        IReadOnlyList<OrderPricingChangedAllocationSet> AllocationSets);

    public record OrderPricingChangedExchangeRate(
        decimal RateOfExchange,
        int NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int? RoundingFactor);

    public record OrderPricingChangedAllocationSet(
        long AllocationSetId,
        PricingAllocationPurpose Purpose,
        int Version,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        long? SupersedesAllocationSetId,
        string? PricingContextRef,
        string? PolicyVersion,
        IReadOnlyList<OrderPricingChangedAllocation> Allocations);

    public record OrderPricingChangedAllocation(
        long AllocationId,
        decimal SaleAmount,
        int SaleCurrencyId,
        long? OrderItemId,
        long? OrderServiceId,
        long? TravellerId,
        long? ItineraryId,
        long? SegmentId,
        string? CoveragePortionRef,
        decimal? OriginalAmount,
        int? OriginalCurrencyId,
        OrderPricingChangedExchangeRate? ExchangeRate,
        long? OriginalAllocationId);
}
