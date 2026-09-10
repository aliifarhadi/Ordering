using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangePlanCoupon(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        long PredecessorOrderServiceId,
        ExchangeCouponDisposition Disposition,
        long SuccessorTicketCouponId,
        long? ReplacementOrderServiceId,
        long? ReplacementOrderSegmentId,
        int? SuccessorCouponNumber = null)
    {
        public bool IsReplaced => Disposition == ExchangeCouponDisposition.Replaced;

        public long ServiceAfterExchange => ReplacementOrderServiceId ?? PredecessorOrderServiceId;
    }
}
