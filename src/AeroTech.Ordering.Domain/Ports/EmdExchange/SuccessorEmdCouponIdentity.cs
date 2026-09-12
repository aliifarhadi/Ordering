using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record SuccessorEmdCouponIdentity(
        int CouponNumber,
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal Value,
        int CurrencyId,
        int? AssociatedTicketCouponNumber = null);
}
