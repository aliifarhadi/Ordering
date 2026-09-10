using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketVoided(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        VoidReason Reason,
        string? ReasonDetail,
        long VoidedBy,
        DateTimeOffset VoidedAt,
        string? ProviderReference,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
