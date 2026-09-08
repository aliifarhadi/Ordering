using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderItemArgs(
        long Id,
        long OrderId,
        ProductType ProductType,
        string? ProductCode,
        string? ProductName,
        decimal Quantity,
        OrderItemUnitOfMeasure UnitOfMeasure,
        DateTimeOffset CreationDate);
}
