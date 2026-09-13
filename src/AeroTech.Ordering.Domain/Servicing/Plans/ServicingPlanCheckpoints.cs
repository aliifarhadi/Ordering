using AeroTech.Messages.Ordering.Enums;

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
        public bool IsReservationConfirmed
            => ReservationOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsDocumentExchangeConfirmed
            => DocumentExchangeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsUnresolved => UnresolvedStage is not null;

        public string? UnresolvedStage
        {
            get
            {
                if (!IsEligibilityEstablished)
                    return nameof(IsEligibilityEstablished);

                if (!IsReservationConfirmed)
                    return nameof(ReservationOutcome);

                if (!IsDocumentExchangeConfirmed)
                    return nameof(DocumentExchangeOutcome);

                if (HasUnsettledMonetary)
                    return nameof(HasUnsettledMonetary);

                if (HasUnsettledFeeDocument)
                    return nameof(HasUnsettledFeeDocument);

                if (HasUnsettledAncillary)
                    return nameof(HasUnsettledAncillary);

                return HasUnresolvedManualReview ? nameof(HasUnresolvedManualReview) : null;
            }
        }
    }
}
