using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
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

        public PricingSource RefundPricingSource { get; set; } = PricingSource.Supplier;

        public bool OmitRefundTerms { get; set; }

        public decimal? RefundAmountOverride { get; set; }

        public int? RefundCurrencyOverride { get; set; }

        public bool OmitRefundDisposition { get; set; }

        public bool OmitRefundSourceReference { get; set; }

        public bool OmitRefundPricingLines { get; set; }

        public bool ReportSelfDerivedRefundPricing { get; set; }

        public long? RefundReversesPricingLineId { get; set; }

        public string ExchangeGroupRef { get; set; } = "EMDX-GROUP-1";

        public Dictionary<string, string> ExchangeGroupByCoupon { get; } = new(StringComparer.Ordinal);

        public ElectronicMiscDocumentType ExchangeSuccessorType { get; set; }
            = ElectronicMiscDocumentType.Associated;

        public string ExchangeSuccessorReasonForIssuanceCode { get; set; } = "A";

        public string ExchangeSuccessorSubCode { get; set; } = "0DF";

        public EmdCouponPurpose ExchangeSuccessorPurpose { get; set; } = EmdCouponPurpose.Fee;

        public int ExchangeCurrencyId { get; set; } = 1;

        public decimal ExchangeSourceValue { get; set; } = 50_000m;

        public decimal ExchangeSuccessorValue { get; set; } = 50_000m;

        public string ExchangeSourceReference { get; set; } = "ANC-EXCHANGE-SOURCE";

        public PricingSource ExchangePricingSource { get; set; } = PricingSource.Supplier;

        public decimal? ExchangeAddCollect { get; set; }

        public string? ExchangeFundingMethodRef { get; set; } = "CARD-ON-FILE";

        public decimal? ExchangeRefundDue { get; set; }

        public decimal? ExchangeResidual { get; set; }

        public ResidualFulfillment ExchangeResidualFulfillment { get; set; }
            = ResidualFulfillment.DocumentCoupled;

        public ResidualInstrumentKind ExchangeResidualInstrument { get; set; } = ResidualInstrumentKind.Emd;

        public int? ExchangeRefundCurrencyOverride { get; set; }

        public bool OmitExchangeRefundDisposition { get; set; }

        public bool MergeExchangeGroupToOneSuccessor { get; set; }

        public long? ExchangeSuccessorOrderServiceId { get; set; }

        public string? ExchangeSuccessorExternalValueReference { get; set; }

        public bool OmitExchangeTerms { get; set; }

        public bool OmitExchangeGroupRef { get; set; }

        public bool OmitExchangeSuccessorCoupons { get; set; }

        public bool OmitExchangeSourceReference { get; set; }

        public bool OmitExchangeFundingMethod { get; set; }

        public bool OmitExchangePricingLines { get; set; }

        public bool ReportSelfDerivedExchangePricing { get; set; }

        public bool OmitExchangeTargetCoupon { get; set; }

        public int? ExchangeTargetCouponOverride { get; set; }

        public bool DuplicateExchangeGroupCoupon { get; set; }

        public bool ConflictExchangeGroupTerms { get; set; }

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
                RefundabilityRule.Refundable,
                ReversesPricingLineId: RefundReversesPricingLineId,
                Code: "ANCILLARY-REFUND",
                Description: "Ancillary refund approved by the pricing source")
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

            var candidates = request.AffectedCoupons
                .Where(coupon => !OmittedCoupons.Contains(Key(coupon)))
                .ToList();

            var exchangeTerms = ExchangeTermsByCoupon(candidates);

            var dispositions = candidates
                .Select(coupon => Decision(coupon, exchangeTerms))
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

        private Dictionary<string, AncillaryEmdExchangeTerms> ExchangeTermsByCoupon(
            IReadOnlyList<AffectedAncillaryCoupon> candidates)
        {
            var terms = new Dictionary<string, AncillaryEmdExchangeTerms>(StringComparer.Ordinal);

            if (OmitExchangeTerms)
                return terms;

            var members = candidates
                .Where(coupon => DispositionOf(coupon) == AncillaryExchangeDisposition.ExchangeToNewEmd)
                .GroupBy(GroupRefOf, StringComparer.Ordinal);

            foreach (var group in members)
            {
                var ordered = group.OrderBy(coupon => coupon.EmdCouponNumber).ToList();
                var successors = ordered.Select(SuccessorCoupon).ToList();

                if (DuplicateExchangeGroupCoupon && successors.Count > 0)
                    successors.Add(successors[0]);

                if (MergeExchangeGroupToOneSuccessor && successors.Count > 1)
                    successors = [successors[0]];

                var shared = new AncillaryEmdExchangeTerms(
                    OmitExchangeGroupRef ? string.Empty : group.Key,
                    ExchangeSuccessorType,
                    ExchangeSuccessorReasonForIssuanceCode,
                    ExchangeCurrencyId,
                    OmitExchangeSuccessorCoupons ? [] : successors,
                    OmitExchangeSourceReference ? string.Empty : ExchangeSourceReference,
                    ReportSelfDerivedExchangePricing
                        ? PricingSource.OrderingDerived
                        : ExchangePricingSource,
                    OmitExchangePricingLines ? [] : ExchangeLines(successors.Count),
                    ExchangeAddCollect is { } addCollect
                        ? new AcceptedAddCollect(addCollect, ExchangeCurrencyId)
                        : null,
                    ExchangeRefundDue is { } refundDue
                        ? new AcceptedRefundDue(
                            refundDue,
                            ExchangeRefundCurrencyOverride ?? ExchangeCurrencyId,
                            OmitExchangeRefundDisposition
                                ? string.Empty
                                : AcceptedRefundDue.OriginalFormOfPayment)
                        : null,
                    ExchangeResidual is { } residual
                        ? new AcceptedResidual(
                            residual,
                            ExchangeCurrencyId,
                            AcceptedRefundDue.OriginalFormOfPayment,
                            ExchangeResidualInstrument,
                            ExchangeResidualFulfillment)
                        : null,
                    OmitExchangeFundingMethod ? null : ExchangeFundingMethodRef);

                var index = 0;

                foreach (var coupon in ordered)
                {
                    terms[Key(coupon)] = ConflictExchangeGroupTerms && index++ > 0
                        ? shared with { SuccessorReasonForIssuanceCode = "Z" }
                        : shared;
                }
            }

            return terms;
        }

        private AncillaryEmdExchangeSuccessorCoupon SuccessorCoupon(AffectedAncillaryCoupon coupon)
            => new(
                ExchangeSuccessorPurpose,
                ExchangeSuccessorSubCode,
                ExchangeSuccessorValue,
                ExchangeCurrencyId,
                OmitExchangeTargetCoupon
                    ? null
                    : ExchangeTargetCouponOverride ?? coupon.PredecessorCouponNumber,
                ExchangeSuccessorPurpose == EmdCouponPurpose.Service
                    ? ExchangeSuccessorOrderServiceId
                    : null,
                ExchangeSuccessorPurpose is EmdCouponPurpose.Deposit or EmdCouponPurpose.ResidualValue
                    ? ExchangeSuccessorExternalValueReference
                    : null);

        private IReadOnlyList<AcceptedRefundPricingLine> ExchangeLines(int successorCount)
        {
            var sourceTotal = ExchangeSourceValue * Math.Max(successorCount, 1);

            var lines = new List<AcceptedRefundPricingLine>
            {
                new(
                    PricingComponentType.Adjustment,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    sourceTotal,
                    ExchangeCurrencyId,
                    sourceTotal,
                    ExchangeCurrencyId,
                    PricingBasisType.OrderService,
                    RefundabilityRule.Refundable,
                    Code: "ANCILLARY-EXCHANGE-OUT",
                    Description: "Ancillary value withdrawn by the approved exchange")
            };

            var successorTotal = ExchangeSuccessorValue * Math.Max(successorCount, 1);

            lines.Add(new AcceptedRefundPricingLine(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                successorTotal,
                ExchangeCurrencyId,
                successorTotal,
                ExchangeCurrencyId,
                PricingBasisType.OrderService,
                RefundabilityRule.Refundable,
                Code: "ANCILLARY-EXCHANGE-IN",
                Description: "Ancillary value granted by the approved exchange"));

            return lines;
        }

        private AncillaryExchangeDisposition DispositionOf(AffectedAncillaryCoupon coupon)
            => DispositionByCoupon.TryGetValue(Key(coupon), out var overridden)
                ? overridden
                : DefaultDisposition;

        private string GroupRefOf(AffectedAncillaryCoupon coupon)
            => ExchangeGroupByCoupon.TryGetValue(Key(coupon), out var overridden)
                ? overridden
                : ExchangeGroupRef;

        private AncillaryCouponDisposition Decision(
            AffectedAncillaryCoupon coupon,
            Dictionary<string, AncillaryEmdExchangeTerms> exchangeTerms)
        {
            var disposition = DispositionOf(coupon);

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
                RefundTerms(disposition),
                disposition == AncillaryExchangeDisposition.ExchangeToNewEmd
                 && exchangeTerms.TryGetValue(Key(coupon), out var terms)
                    ? terms
                    : null);
        }

        private AncillaryRefundTerms? RefundTerms(AncillaryExchangeDisposition disposition)
            => disposition == AncillaryExchangeDisposition.Refund && !OmitRefundTerms
                ? new AncillaryRefundTerms(
                    RefundAmountOverride ?? RefundAmount,
                    RefundCurrencyOverride ?? RefundCurrencyId,
                    OmitRefundDisposition ? string.Empty : RefundDisposition,
                    OmitRefundSourceReference ? string.Empty : RefundSourceReference,
                    ReportSelfDerivedRefundPricing ? PricingSource.OrderingDerived : RefundPricingSource,
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
