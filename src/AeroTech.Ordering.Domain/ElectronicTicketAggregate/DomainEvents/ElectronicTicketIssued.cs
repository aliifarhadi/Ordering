using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketIssued(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicTicketId,
        long OrderId,
        long TravelerId,
        long OperationId,
        string DocumentNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        int CurrencyId,
        decimal IssuedTotal,
        int DocumentVersion,
        long? PredecessorElectronicTicketId,
        IReadOnlyList<ElectronicTicketIssuedCoupon> Coupons) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
