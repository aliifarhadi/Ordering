using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder
{
    public sealed record CancelOrderResult(
        long OrderId,
        OrderStatus Status,
        IReadOnlyList<long> ReleaseTaskIds);
}
