using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder
{
    public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
    {
        private readonly IOrderCancelService _cancelService;
        private readonly IIdentityService _identityService;

        public CancelOrderCommandHandler(IOrderCancelService cancelService, IIdentityService identityService)
        {
            _cancelService = cancelService;
            _identityService = identityService;
        }

        public async Task<CancelOrderResult> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
        {
            var outcome = await _cancelService.CancelAsync(
                command.OrderId,
                command.Reason,
                _identityService.RequiredCurrentUserId,
                command.IdempotencyKey,
                command.ExpectedCommercialVersion,
                cancellationToken);

            return new CancelOrderResult(
                outcome.OrderId,
                outcome.OperationId,
                outcome.Status,
                outcome.CommercialSummary,
                outcome.CommercialVersion,
                outcome.CancelledServiceIds,
                outcome.ReservationReleaseOutcome,
                outcome.OperationStatus,
                outcome.IsReplay);
        }
    }
}
