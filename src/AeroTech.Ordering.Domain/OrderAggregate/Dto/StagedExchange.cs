using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedExchange
    {
        internal StagedExchange(
            StagedPriceChange priceChange,
            OrderSegment segment,
            OrderService service,
            long replacedOrderServiceId,
            long successorElectronicTicketId,
            long successorTicketCouponId,
            IReadOnlyDictionary<string, long> pricingLineIdsBySourceRef)
        {
            PriceChange = priceChange;
            Segment = segment;
            Service = service;
            ReplacedOrderServiceId = replacedOrderServiceId;
            SuccessorElectronicTicketId = successorElectronicTicketId;
            SuccessorTicketCouponId = successorTicketCouponId;
            PricingLineIdsBySourceRef = pricingLineIdsBySourceRef;
        }

        public long ReplacedOrderServiceId { get; }

        public long SuccessorElectronicTicketId { get; }

        public long SuccessorTicketCouponId { get; }

        public long ReplacementOrderServiceId => Service.Id;

        public long ReplacementOrderSegmentId => Segment.Id;

        public IReadOnlyDictionary<string, long> PricingLineIdsBySourceRef { get; }

        internal StagedPriceChange PriceChange { get; }

        internal OrderSegment Segment { get; }

        internal OrderService Service { get; }
    }
}
