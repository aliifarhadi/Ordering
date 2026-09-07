using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    public sealed record FulfillmentOutcome(
        long OrderId,
        OrderStatus OrderStatus,
        IReadOnlyList<FulfillmentTaskOutcome> Tasks);

    public sealed record FulfillmentTaskOutcome(
        long TaskId,
        OrderFulfillmentStatus Status,
        string? LastError);
}
