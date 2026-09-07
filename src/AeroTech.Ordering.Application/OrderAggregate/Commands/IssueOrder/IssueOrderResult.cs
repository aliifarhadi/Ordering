using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.IssueOrder
{
    public sealed record IssueOrderResult(long OrderId, OrderStatus Status, IReadOnlyList<long> TaskIds);
}
