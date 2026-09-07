using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Ordering.Domain.PaymentAggregate.DomainEvents
{
    public sealed record PaymentVoided(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long PaymentId,
        long OrderId,
        decimal Amount,
        decimal TotalVoidedAmount,
        int CurrencyId,
        string ProviderReference) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
