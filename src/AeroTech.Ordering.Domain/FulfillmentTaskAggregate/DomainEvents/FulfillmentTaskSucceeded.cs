using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate.DomainEvents
{
    public sealed record FulfillmentTaskSucceeded(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long TaskId,
        long OrderId,
        OrderFulfillmentTaskType TaskType,
        OrderFulfillmentPurpose Purpose) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
