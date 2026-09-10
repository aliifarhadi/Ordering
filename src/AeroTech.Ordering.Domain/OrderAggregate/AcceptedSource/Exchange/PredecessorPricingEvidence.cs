using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record PredecessorPricingEvidence(
        string CorrelationRef,
        string SourceLineRef,
        string? OccurrenceKey,
        int CouponNumber,
        PricingComponentType ComponentType,
        string? Code,
        decimal SaleAmount,
        int SaleCurrencyId,
        decimal AttributedValue);
}
