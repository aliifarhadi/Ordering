using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record HistoricalUsedCoupon(
        long PredecessorTicketCouponId,
        int CouponNumber,
        TicketCouponFinancialStatus FinancialStatus,
        long CurrentOrderServiceId,
        TicketedSegmentSnapshot IssuedSegment,
        TicketedSegmentSnapshot? CurrentBoundSegment);
}
