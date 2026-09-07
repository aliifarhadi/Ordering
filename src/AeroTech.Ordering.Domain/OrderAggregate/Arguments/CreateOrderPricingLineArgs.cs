using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderPricingLineArgs(
        long Id,
        long OrderId,
        OrderPricingReason LineReason,
        OrderPricingLineScope LineScope,
        OrderPricingLineCategory LineCategory,
        OrderPricingLineSubCategory LineSubCategory,
        OrderPricingLineDirection LineDirection,
        string? Code,
        string? Description,
        string? Reference,
        decimal Amount,
        int CurrencyId,
        bool IsPercentage,
        decimal EquivalentAmount,
        int EquivalentCurrencyId,
        ExchangeRate? ExchangeRate,
        RefundabilityRule Refundability,
        long? OriginalPricingLineId = null);
}
