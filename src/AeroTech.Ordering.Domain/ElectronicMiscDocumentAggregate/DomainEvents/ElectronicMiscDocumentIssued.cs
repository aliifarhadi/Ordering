using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents
{
    public sealed record ElectronicMiscDocumentIssued(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long ElectronicMiscDocumentId,
        long OrderId,
        long? TravelerId,
        long OperationId,
        string DocumentNumber,
        ElectronicMiscDocumentType Type,
        string ReasonForIssuanceCode,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        int CurrencyId,
        decimal IssuedTotal,
        int DocumentVersion,
        IReadOnlyList<ElectronicMiscDocumentIssuedCoupon> Coupons) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
