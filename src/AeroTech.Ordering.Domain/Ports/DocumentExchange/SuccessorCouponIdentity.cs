namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record SuccessorCouponIdentity(
        int PredecessorCouponNumber,
        int CouponNumber);
}
