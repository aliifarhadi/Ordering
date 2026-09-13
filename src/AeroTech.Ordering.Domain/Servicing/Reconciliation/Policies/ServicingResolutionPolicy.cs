using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies
{
    public static class ServicingResolutionPolicy
    {
        public static string ResolutionId(ServicingManualResolutionRequest request)
            => $"{request.Kind}:{request.Reference ?? string.Empty}:{request.OperationId}";

        public static ServicingManualResolution Authorize(
            ServicingManualResolutionRequest request,
            ServicingOperationRecord operation,
            ServicingRecoveryAction recoveryAction,
            DateTimeOffset recordedAt)
        {
            if (string.IsNullOrWhiteSpace(request.Actor) || string.IsNullOrWhiteSpace(request.Reason))
                throw ExceptionFactory.ServicingResolutionAttributionRequired(request.OperationId);

            if (ServicingRecoveryPolicy.IsSettled(operation.Status))
                throw ExceptionFactory.ServicingResolutionOperationIsSettled(
                    operation.OperationId, operation.Status);

            if (operation.ClaimGeneration != request.ExpectedClaimGeneration)
                throw ExceptionFactory.ServicingResolutionClaimGenerationStale(
                    operation.OperationId, operation.ClaimGeneration, request.ExpectedClaimGeneration);

            if (!IsApplicable(request.Kind, recoveryAction))
                throw ExceptionFactory.ServicingResolutionKindNotApplicable(
                    operation.OperationId, recoveryAction, request.Kind);

            return new ServicingManualResolution(
                request.OperationId,
                ResolutionId(request),
                request.Kind,
                request.Actor.Trim(),
                request.Reason.Trim(),
                request.Reference,
                request.EvidenceStage,
                request.ExpectedClaimGeneration,
                recordedAt);
        }

        private static bool IsApplicable(ServicingResolutionKind kind, ServicingRecoveryAction recoveryAction)
            => kind switch
            {
                ServicingResolutionKind.ResumeFromCheckpoint
                    => recoveryAction == ServicingRecoveryAction.ReplayCommand,
                ServicingResolutionKind.RecordManualDecision or ServicingResolutionKind.EscalateExternalAction
                    => recoveryAction == ServicingRecoveryAction.ManualResolutionRequired,
                _ => false
            };
    }
}
