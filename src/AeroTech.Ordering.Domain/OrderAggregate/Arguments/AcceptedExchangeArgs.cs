using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedExchangeArgs(
        AcceptedExchange Accepted,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long SuccessorElectronicTicketId,
        long SuccessorTicketCouponId,
        IReadOnlyCollection<long> PredecessorCarriedPricingLineIds,
        long OperationId,
        long? ActorId = null,
        string? ActorScope = null);
}
