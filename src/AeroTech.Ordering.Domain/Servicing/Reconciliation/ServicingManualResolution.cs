using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
{
    public sealed record ServicingManualResolution(
        long OperationId,
        string ResolutionId,
        ServicingResolutionKind Kind,
        string Actor,
        string Reason,
        string? Reference,
        ServicingEvidenceStage? EvidenceStage,
        long ExpectedClaimGeneration,
        DateTimeOffset RecordedAt);
}
