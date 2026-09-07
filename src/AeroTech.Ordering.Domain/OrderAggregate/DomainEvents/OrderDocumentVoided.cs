using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderDocumentVoided(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long DocumentId,
        string DocumentNumber,
        long OrderId,
        long AirlineOfficeId,
        string? RecordLocator,
        Guid UniqueIdentifierId,
        int Version,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,
        decimal GrandTotal,
        int CurrencyId,
        long CustomerId,
        VoidReason Reason,
        long VoidedBy,
        DateTimeOffset VoidedAt,
        decimal ReversedAmount,
        IReadOnlyList<PricingLineSnapshot> PricingLines) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
