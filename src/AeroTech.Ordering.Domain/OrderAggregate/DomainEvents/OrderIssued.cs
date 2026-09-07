using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderIssued(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        long AirlineOfficeId,
        string? RecordLocator,
        Guid UniqueIdentifierId,
        int Version,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,
        long CustomerId,
        DateTimeOffset IssuedAt,
        decimal GrandTotal,
        int CurrencyId,
        IReadOnlyList<PricingLineSnapshot> PricingLines) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
