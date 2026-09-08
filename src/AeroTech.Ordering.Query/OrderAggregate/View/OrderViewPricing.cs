using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderViewChange(
        long OrderChangeId,
        OrderChangeType ChangeType,
        PricingSource Source,
        long? OperationId,
        string? ExternalReference,
        DateTimeOffset OccurredAt,
        IReadOnlyList<long> ItemIds,
        IReadOnlyList<long> ServiceIds,
        long? PriceChangeSetId);

    public sealed record OrderViewPriceChangeSet(
        long PriceChangeSetId,
        long OrderChangeId,
        long FinancialSequence,
        int ExpectedCommercialVersion,
        PriceChangeReason Reason,
        PricingSource Source,
        string? SourceOfferId,
        string? SourcePricingReference,
        DateTimeOffset CreatedAt,
        DateTimeOffset CommittedAt,
        decimal DerivedCustomerBalanceImpact,
        IReadOnlyList<OrderViewPricingLine> PricingLines);

    public sealed record OrderViewPricingLine(
        long PricingLineId,
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        OrderViewExchangeRate? ExchangeRate,
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
        IReadOnlyList<OrderViewAllocationSet> AllocationSets);

    public sealed record OrderViewExchangeRate(
        decimal RateOfExchange,
        int NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int? RoundingFactor);

    public sealed record OrderViewAllocationSet(
        long AllocationSetId,
        PricingAllocationPurpose Purpose,
        int Version,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        long? SupersedesAllocationSetId,
        string? PricingContextRef,
        string? PolicyVersion,
        IReadOnlyList<OrderViewAllocation> Allocations);

    public sealed record OrderViewAllocation(
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
        OrderViewExchangeRate? ExchangeRate,
        long? OriginalAllocationId);
}
