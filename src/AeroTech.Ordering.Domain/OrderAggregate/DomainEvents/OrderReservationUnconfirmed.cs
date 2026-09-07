using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderReservationUnconfirmed(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long CustomerId,
        long AirlineOfficeId,
        OrderStatus Status,
        FulfillmentFailureReason Reason,
        string Detail) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
