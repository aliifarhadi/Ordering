using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.UpdateLastTicketingDate
{
    public sealed record UpdateLastTicketingDateCommand(
        long OrderId,
        DateTimeOffset LastTicketingDate) : IRequest<Unit>;
}
