using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedScopeCancellationArgs(
        AcceptedScopeCancellation Accepted,
        OrderChangeType Intent,
        long OperationId,
        long? OrderItemId = null,
        long? ActorId = null,
        string? ActorScope = null,
        string? ExternalReference = null);
}
