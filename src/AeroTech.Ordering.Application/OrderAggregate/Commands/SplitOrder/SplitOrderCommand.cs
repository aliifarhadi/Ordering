using AeroTech.Ordering.Application.OrderAggregate.Services.Split;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder
{
    public sealed record SplitOrderCommand(
        long OrderId,
        IReadOnlyCollection<long> TravellerIds) : IRequest<SplitOrderResult>;
}
