using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicAncillaryDispositionAdapter : IAncillaryExchangeDispositionPort
    {
        public string DecisionReference { get; set; } = "ANC-DISPOSITION";

        public int DecisionVersion { get; set; } = 1;

        public AncillaryExchangeDisposition DefaultDisposition { get; set; }
            = AncillaryExchangeDisposition.ReassociateExisting;

        public Dictionary<string, AncillaryExchangeDisposition> DispositionByCoupon { get; }
            = new(StringComparer.Ordinal);

        public Dictionary<string, int?> TargetByCoupon { get; } = new(StringComparer.Ordinal);

        public HashSet<string> OmittedCoupons { get; } = new(StringComparer.Ordinal);

        public bool OmitTargetCoupon { get; set; }

        public bool OmitDecisionReference { get; set; }

        public bool DuplicateFirstDecision { get; set; }

        public bool AddUnrelatedDecision { get; set; }

        public bool ReportUnrelatedPredecessorCoupon { get; set; }

        public bool ReportUnrelatedPredecessorDocument { get; set; }

        public bool Throw { get; set; }

        public List<AncillaryExchangeDispositionRequest> ObservedRequests { get; } = new();

        public Task<AncillaryExchangeDispositionResult> DecideAsync(
            AncillaryExchangeDispositionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ObservedRequests.Add(request);

            if (Throw)
                throw new InvalidOperationException("The ancillary disposition source is unreachable.");

            var dispositions = request.AffectedCoupons
                .Where(coupon => !OmittedCoupons.Contains(Key(coupon)))
                .Select(Decision)
                .ToList();

            if (DuplicateFirstDecision && dispositions.Count > 0)
                dispositions.Add(dispositions[0]);

            if (AddUnrelatedDecision)
                dispositions.Add(new AncillaryCouponDisposition(
                    "EMD-UNRELATED",
                    1,
                    request.PredecessorDocumentNumber,
                    request.ReissueScopeCouponNumbers.FirstOrDefault(),
                    AncillaryExchangeDisposition.ReassociateExisting,
                    request.ReissueScopeCouponNumbers.FirstOrDefault()));

            return Task.FromResult(new AncillaryExchangeDispositionResult(
                OmitDecisionReference ? string.Empty : DecisionReference,
                DecisionVersion,
                dispositions));
        }

        private AncillaryCouponDisposition Decision(AffectedAncillaryCoupon coupon)
        {
            var disposition = DispositionByCoupon.TryGetValue(Key(coupon), out var overridden)
                ? overridden
                : DefaultDisposition;

            return new AncillaryCouponDisposition(
                coupon.EmdDocumentNumber,
                coupon.EmdCouponNumber,
                ReportUnrelatedPredecessorDocument
                    ? $"{coupon.PredecessorDocumentNumber}-OTHER"
                    : coupon.PredecessorDocumentNumber,
                ReportUnrelatedPredecessorCoupon
                    ? coupon.PredecessorCouponNumber + 90
                    : coupon.PredecessorCouponNumber,
                disposition,
                Target(coupon));
        }

        private int? Target(AffectedAncillaryCoupon coupon)
        {
            if (TargetByCoupon.TryGetValue(Key(coupon), out var overridden))
                return overridden;

            return OmitTargetCoupon ? null : coupon.PredecessorCouponNumber;
        }

        private static string Key(AffectedAncillaryCoupon coupon)
            => $"{coupon.EmdDocumentNumber}:{coupon.EmdCouponNumber}";
    }
}
