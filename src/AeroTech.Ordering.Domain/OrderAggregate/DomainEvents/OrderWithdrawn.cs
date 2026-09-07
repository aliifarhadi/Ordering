using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderWithdrawn(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long AirlineOfficeId,
        long CustomerId,
        int CommercialVersion,
        CommercialSummary CommercialSummary,
        VoidReason Reason,
        long WithdrawnBy,
        IReadOnlyList<long> WithdrawnServiceIds) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
