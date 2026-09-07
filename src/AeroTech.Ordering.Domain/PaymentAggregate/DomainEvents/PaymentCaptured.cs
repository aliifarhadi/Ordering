using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Ordering.Domain.PaymentAggregate.DomainEvents
{
    public sealed record PaymentCaptured(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long PaymentId,
        long OrderId,
        decimal Amount,
        int CurrencyId,
        string ProviderReference) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
