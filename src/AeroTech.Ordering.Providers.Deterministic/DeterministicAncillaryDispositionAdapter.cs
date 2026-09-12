using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
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

        public string? QuotedExchangeIdOverride { get; set; }

        public string? ContextFingerprintOverride { get; set; }

        public bool Throw { get; set; }

        public decimal RefundAmount { get; set; } = 50_000m;

        public int RefundCurrencyId { get; set; } = 1;

        public string RefundDisposition { get; set; } = "OriginalFormOfPayment";

        public string RefundSourceReference { get; set; } = "ANC-REFUND-SOURCE";

        public bool OmitRefundTerms { get; set; }

        public decimal? RefundAmountOverride { get; set; }

        public int? RefundCurrencyOverride { get; set; }

        public bool OmitRefundDisposition { get; set; }

        public bool OmitRefundSourceReference { get; set; }

        public bool OmitRefundPricingLines { get; set; }

        private IReadOnlyList<AcceptedRefundPricingLine> RefundLines() =>
        [
            new(
                PricingComponentType.Adjustment,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Credit,
                PricingLineRole.Adjustment,
                RefundAmountOverride ?? RefundAmount,
                RefundCurrencyOverride ?? RefundCurrencyId,
                RefundAmountOverride ?? RefundAmount,
                RefundCurrencyOverride ?? RefundCurrencyId,
                PricingBasisType.OrderService,
                RefundabilityRule.Refundable)
        ];

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
                QuotedExchangeIdOverride ?? request.QuotedExchangeId,
                ContextFingerprintOverride ?? request.ContextFingerprint,
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
                Target(coupon),
                RefundTerms(disposition));
        }

        private AncillaryRefundTerms? RefundTerms(AncillaryExchangeDisposition disposition)
            => disposition == AncillaryExchangeDisposition.Refund && !OmitRefundTerms
                ? new AncillaryRefundTerms(
                    RefundAmountOverride ?? RefundAmount,
                    RefundCurrencyOverride ?? RefundCurrencyId,
                    OmitRefundDisposition ? string.Empty : RefundDisposition,
                    OmitRefundSourceReference ? string.Empty : RefundSourceReference,
                    OmitRefundPricingLines ? [] : RefundLines())
                : null;

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
