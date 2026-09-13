using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation
{
    public sealed record ServicingResolutionExecution(
        long OperationId,
        ServicingResolutionKind Kind,
        string Actor,
        string Reason,
        long ExpectedClaimGeneration,
        string? Reference = null,
        ServicingEvidenceStage? EvidenceStage = null);
}
