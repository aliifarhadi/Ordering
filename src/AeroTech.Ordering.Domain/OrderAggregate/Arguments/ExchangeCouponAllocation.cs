namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record ExchangeCouponAllocation(
        long PredecessorTicketCouponId,
        long SuccessorTicketCouponId,
        long? ReplacementOrderServiceId,
        long? ReplacementOrderSegmentId);
}
