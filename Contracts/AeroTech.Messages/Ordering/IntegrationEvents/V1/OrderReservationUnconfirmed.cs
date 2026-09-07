using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderReservationUnconfirmed(
        long OrderId,
        long CustomerId,
        long AirlineOfficeId,
        OrderStatus Status,
        FulfillmentFailureReason Reason,
        string Detail) : BaseIntegrationEvent;
}
