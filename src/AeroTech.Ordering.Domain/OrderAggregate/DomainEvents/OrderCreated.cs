using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderCreated(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        Guid UniqueIdentifierId,
        long CustomerId,
        long AirlineOfficeId,
        long CreatorUserId,
        SalesChannel Channel,
        OrderStatus Status,
        OrderType Type,
        int CurrencyId,
        int Pax,
        decimal GrandTotal,
        decimal TotalTax,
        decimal CommissionAmount,
        decimal CommissionRate,
        int CommercialVersion,
        long? LinkedOrderId,
        string? LinkedPNR,
        DateTimeOffset? TimeToLive,
        DateTimeOffset CreationDate) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
