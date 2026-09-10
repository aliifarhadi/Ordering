using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangePlanCoupon(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        long PredecessorOrderServiceId,
        ExchangeCouponDisposition Disposition,
        long SuccessorTicketCouponId,
        TicketedSegmentSnapshot TicketedSegment,
        long? ReplacementOrderServiceId,
        long? ReplacementOrderSegmentId,
        int? SuccessorCouponNumber = null)
    {
        public bool IsReplaced => Disposition == ExchangeCouponDisposition.Replaced;

        public long ServiceAfterExchange => ReplacementOrderServiceId ?? PredecessorOrderServiceId;
    }
}
