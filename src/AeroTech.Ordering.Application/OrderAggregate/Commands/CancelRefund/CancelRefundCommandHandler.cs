using AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelRefund
{
    public sealed class CancelRefundCommandHandler : IRequestHandler<CancelRefundCommand, CancelRefundOutcome>
    {
        private readonly ICancelRefundService _cancelRefundService;

        public CancelRefundCommandHandler(ICancelRefundService cancelRefundService) => _cancelRefundService = cancelRefundService;

        public Task<CancelRefundOutcome> Handle(CancelRefundCommand command, CancellationToken cancellationToken)
            => _cancelRefundService.CancelRefundAsync(
                new CancelRefundExecution(
                    command.OrderId,
                    command.ElectronicTicketId,
                    command.RefundRecordId,
                    command.Reason,
                    command.IdempotencyKey,
                    command.ExpectedCommercialVersion,
                    command.ReasonDetail),
                cancellationToken);
    }
}
