using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.WithdrawOrder
{
    public sealed class WithdrawOrderCommandHandler : IRequestHandler<WithdrawOrderCommand, WithdrawOrderOutcome>
    {
        private readonly IWithdrawOrderService _withdrawOrderService;

        public WithdrawOrderCommandHandler(IWithdrawOrderService withdrawOrderService) => _withdrawOrderService = withdrawOrderService;

        public Task<WithdrawOrderOutcome> Handle(WithdrawOrderCommand command, CancellationToken cancellationToken)
            => _withdrawOrderService.WithdrawAsync(command.OrderId, command.Reason, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
