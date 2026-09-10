using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketRevalidated(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long RevalidationRecordId,
        long TicketCouponId,
        int CouponNumber,
        long PreviousOrderServiceId,
        long NewOrderServiceId,
        string QuotedChangeId,
        string TargetSelectionRef,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
