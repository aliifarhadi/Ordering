using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderSplit(
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
        DateTimeOffset SplitAt,
        IReadOnlyList<long> MovedTravellerIds,
        bool WasTicketed,
        decimal GrandTotal,
        int CurrencyId,
        IReadOnlyList<PricingLineSnapshot> PricingLines,
        SplitNewOrderSnapshot NewOrder) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
