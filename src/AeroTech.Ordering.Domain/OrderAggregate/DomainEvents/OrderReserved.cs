using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderReserved(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        string RecordLocator,
        OrderStatus Status,
        long CustomerId,
        long AirlineOfficeId,
        SalesChannel Channel,
        int CurrencyId,
        int Pax,
        decimal GrandTotal,
        decimal TotalTax,
        DateTimeOffset CreationDate) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
