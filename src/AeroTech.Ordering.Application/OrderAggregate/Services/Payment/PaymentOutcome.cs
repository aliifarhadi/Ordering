using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Payment
{
    public sealed record PaymentOutcome(
        long OrderId,
        OrderStatus Status,
        long PaymentId,
        string? PaymentReference,
        FulfillmentFailureReason? FailureReason);
}
