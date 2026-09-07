using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderRemarkModified(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long RemarkId) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
