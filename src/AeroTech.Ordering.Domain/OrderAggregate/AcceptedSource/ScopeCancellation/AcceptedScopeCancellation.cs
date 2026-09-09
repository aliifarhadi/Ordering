using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation
{
    public sealed record AcceptedScopeCancellation(
        string SourceSystem,
        string QuotedCancellationId,
        PricingSource PricingSource,
        long OrderId,
        int ExpectedCommercialVersion,
        OrderChangeType Intent,
        int SaleCurrencyId,
        IReadOnlyList<long> CancelledOrderServiceIds,
        IReadOnlyList<AcceptedCancellationPricingLine> PricingLines,
        string? SourcePricingReference = null);

    public sealed record AcceptedCancellationPricingLine(
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        PricingBasisType BasisType,
        RefundabilityRule Refundability,
        long? BasisReferenceId = null,
        long? OrderItemId = null,
        long? ReversesPricingLineId = null,
        string? Code = null,
        string? Description = null,
        ExchangeRate? ExchangeRate = null,
        PricingApplicationLevel? ApplicationLevel = null,
        string? SourceLineRef = null,
        string? OccurrenceKey = null,
        string? SettlementPartyRef = null,
        string? SettlementCategory = null);
}
