using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketIssued(
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
        IReadOnlyList<ElectronicTicketIssuedCoupon> Coupons) : BaseIntegrationEvent;
}
