using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderProductAdded(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long OrderChangeId,
        long OrderItemId,
        IReadOnlyList<long> OrderServiceIds,
        long PriceChangeSetId,
        long FinancialSequence,
        int CommercialVersion,
        int EventOrdinal,
        long ObligationVersion,
        decimal CustomerTotal,
        int CurrencyId) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
