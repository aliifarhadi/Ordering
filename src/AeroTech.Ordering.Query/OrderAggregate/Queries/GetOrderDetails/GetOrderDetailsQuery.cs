using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails
{
    public sealed record GetOrderDetailsQuery(long OrderId) : IRequest<OrderView?>;
}
