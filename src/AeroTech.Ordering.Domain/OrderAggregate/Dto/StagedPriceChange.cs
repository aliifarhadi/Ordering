using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    internal sealed record StagedPriceChange(
        OrderChange Change,
        OrderPriceChangeSet ChangeSet,
        IReadOnlyList<OrderPricingLine> Lines);
}
