using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedSourcePricingLine(
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        PricingBasisType BasisType,
        PricingApplicationLevel ApplicationLevel,
        RefundabilityRule Refundability,
        string SourceLineRef,
        string OccurrenceKey,
        string? ProductRef,
        string? ServiceRef,
        string? Code,
        string? Description,
        ExchangeRate? ExchangeRate);
}
