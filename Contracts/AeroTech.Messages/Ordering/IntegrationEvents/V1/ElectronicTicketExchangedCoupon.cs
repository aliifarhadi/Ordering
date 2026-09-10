namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicTicketExchangedCoupon(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        long SuccessorTicketCouponId,
        int SuccessorCouponNumber,
        long PreviousOrderServiceId,
        long SuccessorOrderServiceId);
}
