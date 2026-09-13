using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
{
    public sealed record ServicingManualResolutionRequest(
        long OperationId,
        ServicingResolutionKind Kind,
        string Actor,
        string Reason,
        string? Reference,
        ServicingEvidenceStage? EvidenceStage,
        long ExpectedClaimGeneration);
}
