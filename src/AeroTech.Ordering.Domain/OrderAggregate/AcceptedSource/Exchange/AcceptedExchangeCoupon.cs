using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedExchangeCoupon(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        long PredecessorOrderServiceId,
        ExchangeCouponDisposition Disposition,
        AcceptedChangeReplacement? Replacement,
        AcceptedSuccessorCoupon Successor)
    {
        public bool IsReplaced => Disposition == ExchangeCouponDisposition.Replaced;
    }
}
