using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Plans;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies
{
    public static class ServicingRecoveryPolicy
    {
        public static bool IsSettled(ServicingOperationStatus status)
            => status is ServicingOperationStatus.Completed or ServicingOperationStatus.Rejected;

        public static IReadOnlyList<string> ManualReviewReasons(AcceptedExchangePlan? plan)
            => plan is null
                ? []
                : plan.AncillaryManualReviews
                    .Select(review => review.ManualReviewReason)
                    .Where(reason => !string.IsNullOrWhiteSpace(reason))
                    .Select(reason => reason!)
                    .ToList();

        public static string? UnresolvedStage(
            ServicingOperationSnapshot operation,
            AcceptedExchangePlan? plan,
            IReadOnlyList<ServicingExternalEvidence> evidence)
        {
            if (IsSettled(operation.Status))
                return null;

            return plan is not null
                ? ExchangeStage(plan)
                : evidence.FirstOrDefault(candidate => candidate.IsUnresolved)?.Stage.ToString()
                  ?? evidence.LastOrDefault()?.Stage.ToString();
        }

        public static ServicingRecoveryAction Determine(
            ServicingOperationSnapshot operation,
            AcceptedExchangePlan? plan,
            IReadOnlyList<ServicingExternalEvidence> evidence,
            IReadOnlyList<string> manualReviewReasons)
        {
            if (IsSettled(operation.Status))
                return ServicingRecoveryAction.NoneRequired;

            if (operation.Status == ServicingOperationStatus.AwaitingExternal)
                return ServicingRecoveryAction.ReplayCommand;

            if (operation.Status != ServicingOperationStatus.NeedsReconciliation)
                return ServicingRecoveryAction.NoneRequired;

            if (evidence.Any(candidate => candidate.IsUnresolved))
                return ServicingRecoveryAction.ReplayCommand;

            if (manualReviewReasons.Count > 0)
                return ServicingRecoveryAction.ManualResolutionRequired;

            return plan is not null && ExchangeStage(plan) is not null
                ? ServicingRecoveryAction.ReplayCommand
                : ServicingRecoveryAction.ManualResolutionRequired;
        }

        private static string? ExchangeStage(AcceptedExchangePlan plan)
        {
            if (!plan.IsEligibilityEstablished)
                return nameof(plan.EligibilityOutcome);

            if (!plan.IsReservationConfirmed)
                return nameof(plan.ReservationOutcome);

            if (!plan.IsDocumentExchangeConfirmed)
                return nameof(plan.DocumentExchangeOutcome);

            if (plan.RequiresMonetarySettlement && !plan.IsMonetarySettled)
                return nameof(plan.MonetaryOutcome);

            if (plan.RequiresFeeDocumentation && !plan.IsFeeDocumentationSettled)
                return nameof(plan.FeeDocuments);

            if (plan.RequiresAncillaryReassociation && !plan.IsAncillarySettled)
                return nameof(plan.Ancillaries);

            return plan.HasUnresolvedManualReview
                ? nameof(AncillaryExchangeDisposition.ManualReview)
                : null;
        }
    }
}
