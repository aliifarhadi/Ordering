using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed record InlineReservationOutcome(
        OrderStatus Status,
        string? RecordLocator,
        long? ReserveTaskId,
        FulfillmentFailureReason? FailureReason);
}
