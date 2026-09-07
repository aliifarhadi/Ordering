using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder
{
    public sealed record IssueOrderCommand(long OrderId) : IRequest<IssueOrderResult>;
}
