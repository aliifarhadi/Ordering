namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryExchangeDispositionRequest(
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string PredecessorDocumentNumber,
        IReadOnlyList<int> ReissueScopeCouponNumbers,
        IReadOnlyList<AffectedAncillaryCoupon> AffectedCoupons,
        string ContextFingerprint);
}
