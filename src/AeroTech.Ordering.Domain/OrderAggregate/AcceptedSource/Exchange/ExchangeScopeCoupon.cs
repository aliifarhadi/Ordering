using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record ExchangeScopeCoupon(
        long PredecessorTicketCouponId,
        int CouponNumber,
        long CurrentOrderServiceId,
        bool ServiceIsChanging,
        TicketedSegmentSnapshot CurrentSegment,
        TicketedSegmentSnapshot IssuedSegment);
}
