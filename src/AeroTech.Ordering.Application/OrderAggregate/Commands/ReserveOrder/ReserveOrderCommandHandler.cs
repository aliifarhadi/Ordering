using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder
{
    public sealed class ReserveOrderCommandHandler : IRequestHandler<ReserveOrderCommand, ReserveOrderResult>
    {
        private readonly IOrderReservationService _reservationService;

        public ReserveOrderCommandHandler(IOrderReservationService reservationService) => _reservationService = reservationService;

        public async Task<ReserveOrderResult> Handle(ReserveOrderCommand command, CancellationToken cancellationToken)
        {
            var outcome = await _reservationService.ReserveAsync(command.OrderId, cancellationToken);
            return new ReserveOrderResult(outcome.OrderId, outcome.OrderStatus, outcome.Tasks.Select(task => task.TaskId).ToList());
        }
    }
}
