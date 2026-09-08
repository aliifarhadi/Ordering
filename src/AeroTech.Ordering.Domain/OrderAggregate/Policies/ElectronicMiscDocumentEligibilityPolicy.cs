using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public sealed record MiscellaneousDocumentIssueEvidence(
        bool DocumentStockAvailable,
        FundingCoverageOutcome? FundingOutcome,
        decimal ConfirmedFundingAmount,
        IReadOnlyCollection<long> AlreadyDocumentedServiceIds);

    public static class ElectronicMiscDocumentEligibilityPolicy
    {
        public static EligibilityDecision Evaluate(Order order, MiscellaneousDocumentIssueEvidence evidence)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(evidence);

            if (order.CommercialSummary != CommercialSummary.Active)
                return EligibilityDecision.Denied(EligibilityReasonCodes.OrderNotCommerciallyActive);

            var required = order.RequiredElectronicMiscDocumentServiceIds().ToHashSet();

            if (required.Count == 0)
                return EligibilityDecision.Denied(EligibilityReasonCodes.NoEligibleServices);

            var scope = required.Except(evidence.AlreadyDocumentedServiceIds).ToList();

            if (scope.Count == 0)
                return EligibilityDecision.Denied(EligibilityReasonCodes.AlreadyIssued);

            var reasons = new List<string>();

            foreach (var service in order.OrderServices.Where(service => scope.Contains(service.Id)))
                if (service.Status is not (OrderServiceStatus.Active or OrderServiceStatus.Fulfilled))
                    reasons.Add(EligibilityReasonCodes.ServiceNotActive);

            if (!evidence.DocumentStockAvailable)
                reasons.Add(EligibilityReasonCodes.DocumentStockUnavailable);

            var fundingReason = FundingReason(order, evidence);

            if (fundingReason is not null)
                reasons.Add(fundingReason);

            if (reasons.Count == 0)
                return EligibilityDecision.Allowed(scope);

            var distinct = reasons.Distinct().ToArray();

            return IsPendingEvidence(distinct)
                ? EligibilityDecision.PendingEvidence(distinct)
                : EligibilityDecision.Denied(distinct);
        }

        private static string? FundingReason(Order order, MiscellaneousDocumentIssueEvidence evidence) => evidence.FundingOutcome switch
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
                or EligibilityReasonCodes.FundingNotVerified);
    }
}
