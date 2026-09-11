using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public interface IAncillaryExchangeDispositionPort
    {
        Task<AncillaryExchangeDispositionResult> DecideAsync(
            AncillaryExchangeDispositionRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record AncillaryExchangeDispositionRequest(
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string PredecessorDocumentNumber,
        IReadOnlyList<int> ReissueScopeCouponNumbers,
        IReadOnlyList<AffectedAncillaryCoupon> AffectedCoupons);

    public sealed record AffectedAncillaryCoupon(
        string EmdDocumentNumber,
        int EmdCouponNumber,
        ElectronicMiscDocumentType EmdType,
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber);

    public sealed record AncillaryExchangeDispositionResult(
        string DecisionReference,
        int DecisionVersion,
        IReadOnlyList<AncillaryCouponDisposition> Dispositions,
        string? Detail = null);

    public sealed record AncillaryCouponDisposition(
        string EmdDocumentNumber,
        int EmdCouponNumber,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        AncillaryExchangeDisposition Disposition,
        int? TargetPredecessorCouponNumber,
        string? Detail = null);
}
