using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeCouponOutcome(
        long PredecessorTicketCouponId,
        int PredecessorCouponNumber,
        ExchangeCouponDisposition Disposition,
        long OrderServiceId,
        long? ReplacedOrderServiceId,
        long? SuccessorTicketCouponId,
        int? SuccessorCouponNumber);
}
