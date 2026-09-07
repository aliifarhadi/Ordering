using AeroTech.Ordering.Application.OrderAggregate.Services.Payment;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.PayOrder
{
    public sealed class PayOrderCommandHandler : IRequestHandler<PayOrderCommand, PayOrderResult>
    {
        private readonly IPaymentService _paymentService;

        public PayOrderCommandHandler(IPaymentService paymentService) => _paymentService = paymentService;

        public async Task<PayOrderResult> Handle(PayOrderCommand command, CancellationToken cancellationToken)
        {
            var outcome = await _paymentService.PayAsync(command.OrderId, command.FormOfPayment, command.WalletReference, cancellationToken);

            return new PayOrderResult(
                outcome.OrderId,
                outcome.Status,
                outcome.PaymentId,
                outcome.PaymentReference,
                outcome.FailureReason);
        }
    }
}
