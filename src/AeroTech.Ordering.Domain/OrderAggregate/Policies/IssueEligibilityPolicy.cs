using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public sealed record IssueEvidence(
        bool DocumentStockAvailable,
        FundingCoverageOutcome? FundingOutcome,
        decimal ConfirmedFundingAmount,
        IReadOnlyCollection<long> ConfirmedReservationServiceIds,
        IReadOnlyCollection<long> KnownReservationServiceIds,
        IReadOnlyCollection<long> UnknownReservationServiceIds,
        IReadOnlyCollection<long> AlreadyDocumentedServiceIds);

    public static class IssueEligibilityPolicy
    {
        public static EligibilityDecision Evaluate(Order order, IssueEvidence evidence)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(evidence);

            if (order.CommercialSummary != CommercialSummary.Active)
                return EligibilityDecision.Denied(EligibilityReasonCodes.OrderNotCommerciallyActive);

            var candidates = order.OrderServices
                .Where(service => service.RequiresDocument && service.Status != OrderServiceStatus.Cancelled)
                .ToList();

            if (candidates.Count == 0)
                return EligibilityDecision.Denied(EligibilityReasonCodes.NoEligibleServices);

            if (candidates.All(service => evidence.AlreadyDocumentedServiceIds.Contains(service.Id)))
                return EligibilityDecision.Denied(EligibilityReasonCodes.TicketAlreadyIssued);

            var scope = candidates
                .Where(service => !evidence.AlreadyDocumentedServiceIds.Contains(service.Id))
                .ToList();

            var reasons = new List<string>();

            foreach (var service in scope)
            {
                if (service.Status is not (OrderServiceStatus.Active or OrderServiceStatus.Fulfilled))
                    reasons.Add(EligibilityReasonCodes.ServiceNotActive);

                if (!evidence.KnownReservationServiceIds.Contains(service.Id))
                    reasons.Add(EligibilityReasonCodes.ReservationMissing);
                else if (evidence.UnknownReservationServiceIds.Contains(service.Id))
                    reasons.Add(EligibilityReasonCodes.ReservationUnknown);
                else if (!evidence.ConfirmedReservationServiceIds.Contains(service.Id))
                    reasons.Add(EligibilityReasonCodes.ReservationNotConfirmed);

                if (service is OrderAirTransportService air)
                {
                    if (order.Travellers.All(traveller => traveller.Id != air.TravellerId))
                        reasons.Add(EligibilityReasonCodes.TravelerMissing);

                    if (order.Segments.All(segment => segment.Id != air.OrderSegmentId))
                        reasons.Add(EligibilityReasonCodes.SegmentMissing);
                }
            }

            if (!evidence.DocumentStockAvailable)
                reasons.Add(EligibilityReasonCodes.DocumentStockUnavailable);

            var fundingReason = FundingReason(order, evidence);

            if (fundingReason is not null)
                reasons.Add(fundingReason);

            if (reasons.Count == 0)
                return EligibilityDecision.Allowed(scope.Select(service => service.Id).ToList());

            var distinct = reasons.Distinct().ToArray();

            return IsPendingEvidence(distinct)
                ? EligibilityDecision.PendingEvidence(distinct)
                : EligibilityDecision.Denied(distinct);
        }

        private static string? FundingReason(Order order, IssueEvidence evidence) => evidence.FundingOutcome switch
        {
            null => EligibilityReasonCodes.FundingNotVerified,
            FundingCoverageOutcome.Pending => EligibilityReasonCodes.FundingPending,
            FundingCoverageOutcome.Unknown => EligibilityReasonCodes.FundingUnknown,
            FundingCoverageOutcome.Insufficient => EligibilityReasonCodes.FundingInsufficient,
            FundingCoverageOutcome.Confirmed when evidence.ConfirmedFundingAmount < order.Amount.GrandTotal
                => EligibilityReasonCodes.FundingInsufficient,
            _ => null
        };

        private static bool IsPendingEvidence(IReadOnlyCollection<string> reasons)
            => reasons.All(reason => reason
                is EligibilityReasonCodes.FundingPending
                or EligibilityReasonCodes.FundingUnknown
                or EligibilityReasonCodes.FundingNotVerified
                or EligibilityReasonCodes.ReservationUnknown
                or EligibilityReasonCodes.ReservationMissing
                or EligibilityReasonCodes.ReservationNotConfirmed);
    }
}
