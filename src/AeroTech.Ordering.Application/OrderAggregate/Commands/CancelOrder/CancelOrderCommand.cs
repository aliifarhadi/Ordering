using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder
{
    public sealed record CancelOrderCommand(long OrderId) : IRequest<CancelOrderResult>;
}
