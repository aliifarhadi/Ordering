using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record ExchangedServiceBinding(
        long PredecessorTicketCouponId,
        long SuccessorTicketCouponId,
        ExchangeCouponDisposition Disposition,
        long OrderServiceId,
        long OrderSegmentId,
        long? ReplacedOrderServiceId);
}
