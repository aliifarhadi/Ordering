using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketRefundCancelled(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RefundRecordId,
        long CorrectionRecordId,
        long OriginalRefundOperationId,
        decimal CorrectedAmount,
        int CurrencyId,
        string Reason,
        ElectronicTicketStatus StatusSummary,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
