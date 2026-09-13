using AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RecordServicingResolution
{
    public sealed class RecordServicingResolutionCommandHandler
        : IRequestHandler<RecordServicingResolutionCommand, ServicingResolutionOutcome>
    {
        private readonly IServicingResolutionService _resolutionService;

        public RecordServicingResolutionCommandHandler(IServicingResolutionService resolutionService)
            => _resolutionService = resolutionService;

        public Task<ServicingResolutionOutcome> Handle(
            RecordServicingResolutionCommand command,
            CancellationToken cancellationToken)
            => _resolutionService.RecordAsync(
                new ServicingResolutionExecution(
                    command.OperationId,
                    command.Kind,
                    command.Actor,
                    command.Reason,
                    command.ExpectedClaimGeneration,
                    command.Reference,
                    command.EvidenceStage),
                cancellationToken);
    }
}
