using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderScopeCancelled(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long OwnerAirlineId,
        long CustomerId,
        long OrderChangeId,
        OrderChangeType Intent,
        long? OperationId,
        int CommercialVersion,
        int EventOrdinal,
        CommercialSummary CommercialSummary,
        OrderStatus Status,
        IReadOnlyList<long> CancelledServiceIds,
        IReadOnlyList<long> CancelledItemIds,
        long? PriceChangeSetId,
        int CurrencyId,
        decimal CustomerTotal) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
