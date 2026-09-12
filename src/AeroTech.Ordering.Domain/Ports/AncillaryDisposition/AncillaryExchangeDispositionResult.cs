namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryExchangeDispositionResult(
        string DecisionReference,
        int DecisionVersion,
        string QuotedExchangeId,
        string ContextFingerprint,
        IReadOnlyList<AncillaryCouponDisposition> Dispositions,
        string? Detail = null);
}
