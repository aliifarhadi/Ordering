using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryCouponDisposition(
        string EmdDocumentNumber,
        int EmdCouponNumber,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        AncillaryExchangeDisposition Disposition,
        int? TargetPredecessorCouponNumber,
        AncillaryRefundTerms? Refund = null,
        string? Detail = null);
}
