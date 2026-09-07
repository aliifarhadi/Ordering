using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public sealed record EligibilityDecision(
        EligibilityOutcome Outcome,
        IReadOnlyList<string> ReasonCodes,
        IReadOnlyList<long> EffectiveScopeServiceIds)
    {
        public bool IsAllowed => Outcome == EligibilityOutcome.Allowed;

        public string Reasons => string.Join(", ", ReasonCodes);

        public static EligibilityDecision Allowed(IReadOnlyList<long> scope)
            => new(EligibilityOutcome.Allowed, [], scope);

        public static EligibilityDecision Denied(params string[] reasonCodes)
            => new(EligibilityOutcome.Denied, reasonCodes, []);

        public static EligibilityDecision PendingEvidence(params string[] reasonCodes)
            => new(EligibilityOutcome.PendingEvidence, reasonCodes, []);
    }

    public static class EligibilityReasonCodes
    {
        public const string OrderNotCommerciallyActive = "ORDER_NOT_COMMERCIALLY_ACTIVE";
        public const string NoEligibleServices = "NO_ELIGIBLE_SERVICES";
        public const string ServiceNotActive = "SERVICE_NOT_ACTIVE";
        public const string ServiceAlreadyDocumented = "SERVICE_ALREADY_DOCUMENTED";
        public const string ReservationMissing = "RESERVATION_MISSING";
        public const string ReservationNotConfirmed = "RESERVATION_NOT_CONFIRMED";
        public const string ReservationUnknown = "RESERVATION_UNKNOWN_OUTCOME";
        public const string FundingInsufficient = "FUNDING_INSUFFICIENT";
        public const string FundingPending = "FUNDING_PENDING";
        public const string FundingUnknown = "FUNDING_UNKNOWN";
        public const string FundingNotVerified = "FUNDING_NOT_VERIFIED";
        public const string DocumentStockUnavailable = "DOCUMENT_STOCK_UNAVAILABLE";
        public const string TravelerMissing = "TRAVELER_MISSING";
        public const string SegmentMissing = "SEGMENT_MISSING";
        public const string AlreadyReserved = "ALREADY_RESERVED";
        public const string AlreadyIssued = "ALREADY_ISSUED";
        public const string TicketAlreadyIssued = "TICKET_ALREADY_ISSUED";
    }
}
