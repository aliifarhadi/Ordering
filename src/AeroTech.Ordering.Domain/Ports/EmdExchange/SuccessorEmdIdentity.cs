using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record SuccessorEmdIdentity(
        string DocumentNumber,
        ElectronicMiscDocumentType Type,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        string ReasonForIssuanceCode,
        int CurrencyId,
        IReadOnlyList<SuccessorEmdCouponIdentity> Coupons,
        long? BeneficiaryTravellerId = null,
        string? AssociatedTicketDocumentNumber = null);
}
