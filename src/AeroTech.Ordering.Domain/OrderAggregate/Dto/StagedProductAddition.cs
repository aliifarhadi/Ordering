using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    internal sealed record StagedProductAddition(
        OrderChange Change,
        OrderItem Item,
        IReadOnlyList<OrderService> Services,
        IReadOnlyList<OrderItemServiceLink> Links,
        StagedPriceChange PriceChange);
}
