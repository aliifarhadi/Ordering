using AeroTech.Ordering.Query.OrderAggregate.Dto;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderById
{
    public sealed record GetOrderByIdQuery(long OrderId) : IRequest<OrderDto?>;
}
