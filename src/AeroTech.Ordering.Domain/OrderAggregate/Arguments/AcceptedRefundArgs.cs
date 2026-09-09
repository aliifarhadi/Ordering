using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedRefundArgs(
        AcceptedRefund Accepted,
        IReadOnlyList<long> RefundedOrderServiceIds,
        IReadOnlyCollection<long> DocumentPricingLineIds,
        long OperationId,
        long? ActorId = null,
        string? ActorScope = null);
}
