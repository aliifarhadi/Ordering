namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record PredecessorCouponEvidence(
        long TicketCouponId,
        int CouponNumber,
        long OrderServiceId);
}
