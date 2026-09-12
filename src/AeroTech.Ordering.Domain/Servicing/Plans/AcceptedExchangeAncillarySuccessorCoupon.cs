using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangeAncillarySuccessorCoupon(
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal Value,
        int CurrencyId,
        int? TargetPredecessorCouponNumber = null,
        long? TargetSuccessorTicketCouponId = null,
        long? OrderServiceId = null,
        string? ExternalValueReference = null);
}
