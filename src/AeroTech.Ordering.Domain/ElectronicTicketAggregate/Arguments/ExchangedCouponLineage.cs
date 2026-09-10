namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record ExchangedCouponLineage(
        long PredecessorTicketCouponId,
        long SuccessorTicketCouponId,
        int SuccessorCouponNumber,
        long SuccessorOrderServiceId);
}
