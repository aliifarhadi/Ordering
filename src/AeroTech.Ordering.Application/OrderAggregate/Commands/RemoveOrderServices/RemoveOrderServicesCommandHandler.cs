using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RemoveOrderServices
{
    public sealed class RemoveOrderServicesCommandHandler : IRequestHandler<RemoveOrderServicesCommand, ScopeCancellationOutcome>
    {
        private readonly IOrderScopeCancellationService _scopeCancellationService;

        public RemoveOrderServicesCommandHandler(IOrderScopeCancellationService scopeCancellationService) => _scopeCancellationService = scopeCancellationService;

        public Task<ScopeCancellationOutcome> Handle(RemoveOrderServicesCommand command, CancellationToken cancellationToken)
            => _scopeCancellationService.RemoveServicesAsync(command.OrderId, command.OrderServiceIds, command.QuotedCancellationId, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
