using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RecordServicingResolution
{
    public sealed record RecordServicingResolutionCommand(
        long OperationId,
        ServicingResolutionKind Kind,
        string Actor,
        string Reason,
        long ExpectedClaimGeneration,
        string? Reference = null,
        ServicingEvidenceStage? EvidenceStage = null) : IRequest<ServicingResolutionOutcome>;
}
