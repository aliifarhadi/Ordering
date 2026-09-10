using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrderItem
{
    public sealed class CancelOrderItemCommandHandler : IRequestHandler<CancelOrderItemCommand, ScopeCancellationOutcome>
    {
        private readonly IOrderScopeCancellationService _scopeCancellationService;

        public CancelOrderItemCommandHandler(IOrderScopeCancellationService scopeCancellationService) => _scopeCancellationService = scopeCancellationService;

        public Task<ScopeCancellationOutcome> Handle(CancelOrderItemCommand command, CancellationToken cancellationToken)
            => _scopeCancellationService.CancelItemAsync(command.OrderId, command.OrderItemId, command.QuotedCancellationId, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
