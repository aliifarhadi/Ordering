using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AffectedAncillaryCoupon(
        string EmdDocumentNumber,
        int EmdCouponNumber,
        ElectronicMiscDocumentType EmdType,
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber);
}
