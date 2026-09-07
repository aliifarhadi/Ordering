using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderRemarkAdded(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long RemarkId,
        OrderRemarkType Type,
        OrderRemarkScope Scope) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
