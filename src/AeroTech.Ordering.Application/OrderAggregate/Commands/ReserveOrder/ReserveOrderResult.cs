using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder
{
    public sealed record ReserveOrderResult(long OrderId, OrderStatus Status, IReadOnlyList<long> TaskIds);
}
