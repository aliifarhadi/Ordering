using AeroTech.Framework.Core.Domain.Events;

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
        IReadOnlyList<ElectronicTicketExchangedCoupon> Coupons,
        string QuotedExchangeId,
        string TargetSelectionRef,
        int DocumentVersion) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
