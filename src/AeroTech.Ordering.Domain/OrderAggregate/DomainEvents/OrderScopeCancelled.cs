using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderItemCancelled(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long OwnerAirlineId,
        long CustomerId,
        long OrderChangeId,
        long? OperationId,
        int CommercialVersion,
        int EventOrdinal,
        CommercialSummary CommercialSummary,
        OrderStatus Status,
        IReadOnlyList<long> CancelledOrderItemIds,
        IReadOnlyList<long> CancelledServiceIds,
        long? PriceChangeSetId,
        int CurrencyId,
        decimal CustomerTotal) : DomainEvent(EventId, AggregateId, TimeOfOccurrence)
    {
        public OrderChangeType Intent => OrderChangeType.Cancel;
    }

    public sealed record OrderServicesRemoved(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long OwnerAirlineId,
        long CustomerId,
        long OrderChangeId,
        long? OperationId,
        int CommercialVersion,
        int EventOrdinal,
        CommercialSummary CommercialSummary,
        OrderStatus Status,
        IReadOnlyList<long> RemovedServiceIds,
        IReadOnlyList<long> RolledUpOrderItemIds,
        long? PriceChangeSetId,
        int CurrencyId,
        decimal CustomerTotal) : DomainEvent(EventId, AggregateId, TimeOfOccurrence)
    {
        public OrderChangeType Intent => OrderChangeType.RemoveService;
    }
}
