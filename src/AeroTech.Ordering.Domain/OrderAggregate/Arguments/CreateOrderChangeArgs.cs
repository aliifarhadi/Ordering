using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderChangeArgs(
        long Id,
        long OrderId,
        OrderChangeType ChangeType,
        PricingSource Source,
        DateTimeOffset OccurredAt,
        string? Reason = null,
        string? ExternalReference = null,
        string? ActorScope = null,
        long? ActorId = null,
        long? OperationId = null);
}
