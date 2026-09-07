using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Split
{
    public sealed record SplitOrderResult(
        long SourceOrderId,
        long NewOrderId,
        OrderStatus SourceStatus,
        OrderStatus NewOrderStatus);
}
