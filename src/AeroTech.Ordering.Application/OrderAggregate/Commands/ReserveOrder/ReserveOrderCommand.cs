using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.ReserveOrder
{
    public sealed record ReserveOrderCommand(long OrderId) : IRequest<ReserveOrderResult>;
}
