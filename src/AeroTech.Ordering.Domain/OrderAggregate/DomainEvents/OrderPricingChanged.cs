using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderPricingChanged(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
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
        decimal CustomerBalanceImpact,
        decimal CustomerTotalAfter,
        IReadOnlyList<PricingChangeLine> PricingLines) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);

    public sealed record PricingChangeLine(
        long PricingLineId,
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        PricingChangeExchangeRate? ExchangeRate,
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
        IReadOnlyList<PricingChangeAllocationSet> AllocationSets);

    public sealed record PricingChangeExchangeRate(
        decimal RateOfExchange,
        int NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int? RoundingFactor);

    public sealed record PricingChangeAllocationSet(
        long AllocationSetId,
        PricingAllocationPurpose Purpose,
        int Version,
        PricingSource Source,
        PricingAllocationMethod Method,
        PricingAllocationCompleteness Completeness,
        long? SupersedesAllocationSetId,
        string? PricingContextRef,
        string? PolicyVersion,
        IReadOnlyList<PricingChangeAllocation> Allocations);

    public sealed record PricingChangeAllocation(
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
        PricingChangeExchangeRate? ExchangeRate,
        long? OriginalAllocationId);
}
