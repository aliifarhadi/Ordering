using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.PaymentAggregate.DomainEvents
{
    public sealed record PaymentDeclined(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long PaymentId,
        long OrderId,
        FulfillmentFailureReason Reason,
        string Detail) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
