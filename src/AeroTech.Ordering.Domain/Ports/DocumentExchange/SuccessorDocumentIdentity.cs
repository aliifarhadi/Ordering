using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record SuccessorDocumentIdentity(
        string DocumentNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        DateTimeOffset? VoidDeadline,
        IReadOnlyList<SuccessorCouponIdentity> Coupons);
}
