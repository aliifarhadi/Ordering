using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder.Backoffice
{
    public sealed class BackofficeReserveOrderCommandHandler : IRequestHandler<BackofficeReserveOrderCommand, ReserveOrderOutcome>
    {
        private readonly IReserveOrderService _reserveOrderService;

        public BackofficeReserveOrderCommandHandler(IReserveOrderService reserveOrderService) => _reserveOrderService = reserveOrderService;

        public Task<ReserveOrderOutcome> Handle(BackofficeReserveOrderCommand command, CancellationToken cancellationToken)
            => _reserveOrderService.ReserveAsync(command.OrderId, command.IdempotencyKey, command.ExpectedCommercialVersion, cancellationToken);
    }
}
