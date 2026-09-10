using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicMiscDocumentIssued(
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
        IReadOnlyList<ElectronicMiscDocumentIssuedCoupon> Coupons) : BaseIntegrationEvent;
}
