using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer
{
    public sealed record CreateOrderFromOfferResult(
        long OrderId,
        OrderStatus Status,
        string? RecordLocator,
        FulfillmentFailureReason? ReservationFailureReason,
        long? ReserveTaskId);
}
