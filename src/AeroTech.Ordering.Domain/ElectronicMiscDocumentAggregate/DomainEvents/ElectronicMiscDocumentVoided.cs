using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents
{
    public sealed record ElectronicMiscDocumentVoided(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicMiscDocumentId,
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
