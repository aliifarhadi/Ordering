using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.DomainEvents
{
    public sealed record TrafficDocumentReassigned(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long DocumentId,
        string DocumentNumber,
        long SourceOrderId,
        long NewOrderId,
        long NewTravellerId) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
