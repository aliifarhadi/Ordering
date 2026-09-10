using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RefundDocument
{
    public sealed class RefundDocumentCommandHandler : IRequestHandler<RefundDocumentCommand, RefundOutcome>
    {
        private readonly IRefundService _refundService;

        public RefundDocumentCommandHandler(IRefundService refundService) => _refundService = refundService;

        public Task<RefundOutcome> Handle(RefundDocumentCommand command, CancellationToken cancellationToken)
            => _refundService.RefundAsync(
                new RefundExecution(
                    command.OrderId,
                    command.ElectronicTicketId,
                    command.TicketCouponIds,
                    command.IdempotencyKey,
                    command.ExpectedCommercialVersion,
                    command.QuotedRefundId,
                    command.Manual),
                cancellationToken);
    }
}
