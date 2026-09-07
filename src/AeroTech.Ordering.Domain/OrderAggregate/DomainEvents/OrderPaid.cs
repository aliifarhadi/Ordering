using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.DomainEvents
{
    public sealed record OrderPaid(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long OrderId,
        OrderStatus Status,
        long PaymentId,
        decimal Amount,
        int CurrencyId,
        FormOfPayment FormOfPayment,
        string? ProviderReference,
        DateTimeOffset PaidAt,
        long CustomerId,
        long AirlineOfficeId) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
