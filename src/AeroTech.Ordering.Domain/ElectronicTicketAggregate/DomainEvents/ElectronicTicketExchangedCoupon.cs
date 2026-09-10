namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents
{
    public sealed record ElectronicTicketExchangedCoupon(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        long SuccessorTicketCouponId,
        int SuccessorCouponNumber,
        long PreviousOrderServiceId,
        long SuccessorOrderServiceId);
}
