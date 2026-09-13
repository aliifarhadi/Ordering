using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies
{
    public static class ServicingRecoveryPolicy
    {
        public static bool IsSettled(ServicingOperationStatus status)
            => status is ServicingOperationStatus.Completed or ServicingOperationStatus.Rejected;

        public static bool IsUnresolved(ProviderOperationOutcome? outcome)
            => outcome is null or ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;

        public static ServicingRecoveryAction Determine(
            ServicingOperationStatus status,
            bool hasUnresolvedEvidence,
            bool hasUnresolvedCheckpoint,
            bool hasUnresolvedManualReview)
        {
            if (IsSettled(status))
                return ServicingRecoveryAction.NoneRequired;

            if (status == ServicingOperationStatus.AwaitingExternal)
                return ServicingRecoveryAction.ReplayCommand;

            if (status != ServicingOperationStatus.NeedsReconciliation)
                return ServicingRecoveryAction.NoneRequired;

            if (hasUnresolvedEvidence)
                return ServicingRecoveryAction.ReplayCommand;

            if (hasUnresolvedManualReview)
                return ServicingRecoveryAction.ManualResolutionRequired;

            return hasUnresolvedCheckpoint
                ? ServicingRecoveryAction.ReplayCommand
                : ServicingRecoveryAction.ManualResolutionRequired;
        }
    }
}
