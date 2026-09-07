using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderPricingLineAllocationArgs(
        long Id,
        long? OrderItemId,
        long? OrderServiceId,
        OrderPricingLineAllocationTargetType? TargetType,
        long? TargetId,
        decimal Amount,
        int CurrencyId,
        decimal EquivalentAmount,
        int EquivalentCurrencyId,
        ExchangeRate? ExchangeRate,
        long? OriginalAllocationId = null);
}
