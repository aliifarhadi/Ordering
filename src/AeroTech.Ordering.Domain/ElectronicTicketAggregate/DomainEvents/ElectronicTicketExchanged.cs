using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketExchanged(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        string DocumentNumber,
        long OperationId,
        long ExchangeRecordId,
        long SuccessorElectronicTicketId,
        string SuccessorDocumentNumber,
        long PredecessorTicketCouponId,
        long SuccessorTicketCouponId,
        long PreviousOrderServiceId,
        long ReplacementOrderServiceId,
        string QuotedExchangeId,
        string TargetSelectionRef,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
