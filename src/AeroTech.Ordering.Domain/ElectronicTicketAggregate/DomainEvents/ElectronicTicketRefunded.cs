using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketRefunded(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RefundRecordId,
        string QuotedRefundId,
        PricingSource PricingSource,
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        IReadOnlyList<long> TicketCouponIds,
        ElectronicTicketStatus StatusSummary,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
