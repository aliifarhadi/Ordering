using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryEmdExchangeSuccessorCoupon(
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal Value,
        int CurrencyId,
        int? TargetPredecessorCouponNumber = null,
        long? OrderServiceId = null,
        string? ExternalValueReference = null);
}
