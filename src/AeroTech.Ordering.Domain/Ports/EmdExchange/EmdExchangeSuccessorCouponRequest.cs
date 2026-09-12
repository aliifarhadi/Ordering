using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record EmdExchangeSuccessorCouponRequest(
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal Value,
        int? TargetSuccessorTicketCouponNumber);
}
