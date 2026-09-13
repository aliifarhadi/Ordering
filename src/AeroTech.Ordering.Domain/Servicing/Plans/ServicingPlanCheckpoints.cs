using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ServicingPlanCheckpoints(
        bool IsEligibilityEstablished,
        ProviderOperationOutcome ReservationOutcome,
        ProviderOperationOutcome? DocumentExchangeOutcome,
        bool HasUnsettledMonetary,
        bool HasUnsettledFeeDocument,
        bool HasUnsettledAncillary,
        bool HasUnresolvedManualReview)
    {
        public const string EligibilityStage = "EligibilityOutcome";

        public const string ReservationStage = "ReservationOutcome";

        public const string DocumentExchangeStage = "DocumentExchangeOutcome";

        public const string MonetaryStage = "MonetaryOutcome";

        public const string FeeDocumentStage = "FeeDocuments";

        public const string AncillaryStage = "Ancillaries";

        public const string ManualReviewStage = "ManualReview";

        public static ServicingPlanCheckpoints From(
            bool isEligibilityEstablished,
            ProviderOperationOutcome reservationOutcome,
            ProviderOperationOutcome? documentExchangeOutcome,
            ServicingMonetaryCheckpoint monetary,
            IReadOnlyCollection<DateTimeOffset?> feeDocumentSettlements,
            IReadOnlyCollection<ServicingAncillaryCheckpoint> ancillaries,
            IReadOnlyCollection<ServicingExchangeGroupCheckpoint> exchangeGroups,
            IReadOnlyCollection<DateTimeOffset?> cancelGroupSettlements)
        {
            var executable = ancillaries
                .Where(ancillary => ServicingSettlementRules.IsExecutable(ancillary.Disposition))
                .ToList();

            var ungroupedSettled = executable
                .Where(ancillary => !ServicingSettlementRules.IsGrouped(ancillary.Disposition))
                .All(ancillary => ancillary.IsSettled);

            var ancillarySettled = ungroupedSettled
                                   && exchangeGroups.All(group => group.IsSettled)
                                   && cancelGroupSettlements.All(ServicingSettlementRules.IsCancelGroupSettled);

            return new ServicingPlanCheckpoints(
                isEligibilityEstablished,
                reservationOutcome,
                documentExchangeOutcome,
                monetary.IsRequired && !monetary.IsSettled,
                feeDocumentSettlements.Count > 0
                    && !feeDocumentSettlements.All(ServicingSettlementRules.IsFeeDocumentSettled),
                executable.Count > 0 && !ancillarySettled,
                ancillaries.Any(ancillary =>
                    ancillary.Disposition == AncillaryExchangeDisposition.ManualReview));
        }

        public bool IsReservationConfirmed
            => ServicingSettlementRules.IsConfirmed(ReservationOutcome);

        public bool IsDocumentExchangeConfirmed
            => ServicingSettlementRules.IsConfirmed(DocumentExchangeOutcome);

        public bool IsUnresolved => UnresolvedStage is not null;

        public string? UnresolvedStage
        {
            get
            {
                if (!IsEligibilityEstablished)
                    return EligibilityStage;

                if (!IsReservationConfirmed)
                    return ReservationStage;

                if (!IsDocumentExchangeConfirmed)
                    return DocumentExchangeStage;

                if (HasUnsettledMonetary)
                    return MonetaryStage;

                if (HasUnsettledFeeDocument)
                    return FeeDocumentStage;

                if (HasUnsettledAncillary)
                    return AncillaryStage;

                return HasUnresolvedManualReview ? ManualReviewStage : null;
            }
        }
    }
}
