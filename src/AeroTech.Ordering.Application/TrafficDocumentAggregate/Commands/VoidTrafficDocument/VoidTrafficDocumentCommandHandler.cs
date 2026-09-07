using AeroTech.Framework.Core.ServiceContracts;
using MediatR;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed class VoidTrafficDocumentCommandHandler : IRequestHandler<VoidTrafficDocumentCommand, VoidTrafficDocumentResult>
    {
        private readonly ITrafficDocumentVoidService _voidService;
        private readonly IIdentityService _identityService;

        public VoidTrafficDocumentCommandHandler(ITrafficDocumentVoidService voidService, IIdentityService identityService)
        {
            _voidService = voidService;
            _identityService = identityService;
        }

        public async Task<VoidTrafficDocumentResult> Handle(VoidTrafficDocumentCommand command, CancellationToken cancellationToken)
        {
            var outcome = await _voidService.VoidAsync(
                command.OrderId,
                command.DocumentId,
                command.Reason,
                command.ReasonDetail,
                _identityService.RequiredCurrentUserId,
                cancellationToken);

            return new VoidTrafficDocumentResult(
                command.OrderId,
                outcome.DocumentId,
                outcome.DocumentStatus,
                outcome.FailureReason,
                outcome.VoidTaskId);
        }
    }
}
