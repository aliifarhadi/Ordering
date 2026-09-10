using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedExchangeCoupon
    {
        internal StagedExchangeCoupon(
            long predecessorTicketCouponId,
            long successorTicketCouponId,
            ExchangeCouponDisposition disposition,
            long orderServiceId,
            long orderSegmentId,
            long? replacedOrderServiceId,
            OrderSegment? replacementSegment,
            OrderService? replacementService)
        {
            PredecessorTicketCouponId = predecessorTicketCouponId;
            SuccessorTicketCouponId = successorTicketCouponId;
            Disposition = disposition;
            OrderServiceId = orderServiceId;
            OrderSegmentId = orderSegmentId;
            ReplacedOrderServiceId = replacedOrderServiceId;
            ReplacementSegment = replacementSegment;
            ReplacementService = replacementService;
        }

        public long PredecessorTicketCouponId { get; }

        public long SuccessorTicketCouponId { get; }

        public ExchangeCouponDisposition Disposition { get; }

        public long OrderServiceId { get; }

        public long OrderSegmentId { get; }

        public long? ReplacedOrderServiceId { get; }

        public bool IsReplaced => Disposition == ExchangeCouponDisposition.Replaced;

        internal OrderSegment? ReplacementSegment { get; }

        internal OrderService? ReplacementService { get; }
    }
}
