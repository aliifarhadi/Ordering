using AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid;
using MediatR;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed class VoidTrafficDocumentCommandHandler : IRequestHandler<VoidTrafficDocumentCommand, VoidTrafficDocumentResult>
    {
        private readonly IDocumentVoidService _voidService;

        public VoidTrafficDocumentCommandHandler(IDocumentVoidService voidService) => _voidService = voidService;

        public async Task<VoidTrafficDocumentResult> Handle(
            VoidTrafficDocumentCommand command,
            CancellationToken cancellationToken)
        {
            var outcome = await _voidService.VoidAsync(
                command.OrderId,
                command.DocumentId,
                command.IdempotencyKey,
                cancellationToken);

            return new VoidTrafficDocumentResult(
                outcome.OrderId,
                outcome.OperationId,
                outcome.DocumentKind,
                outcome.DocumentId,
                outcome.DocumentNumber,
                outcome.DocumentVersion,
                outcome.AffectedOrderServiceIds,
                outcome.ProviderOutcome,
                outcome.OperationStatus,
                outcome.RefundRequiredInstead,
                outcome.IsReplay);
        }
    }
}
