using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderPaymentUnconfirmed(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        OrderStatus Status,
        long PaymentId,
        FulfillmentFailureReason Reason,
        string Detail) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
