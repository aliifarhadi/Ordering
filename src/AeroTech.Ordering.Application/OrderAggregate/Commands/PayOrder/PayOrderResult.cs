using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.PayOrder
{
    public sealed record PayOrderResult(
        long OrderId,
        OrderStatus Status,
        long PaymentId,
        string? PaymentReference,
        FulfillmentFailureReason? FailureReason);
}
