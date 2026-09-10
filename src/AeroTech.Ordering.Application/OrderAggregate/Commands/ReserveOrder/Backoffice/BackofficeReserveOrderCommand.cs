using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder.Backoffice
{
    public sealed record BackofficeReserveOrderCommand(
        long OrderId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<ReserveOrderOutcome>;
}
