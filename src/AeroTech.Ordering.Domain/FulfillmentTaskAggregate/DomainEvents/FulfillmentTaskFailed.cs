using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate.DomainEvents
{
    public sealed record FulfillmentTaskFailed(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long TaskId,
        long OrderId,
        OrderFulfillmentTaskType TaskType,
        OrderFulfillmentPurpose Purpose,
        string Error) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
