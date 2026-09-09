using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedVoluntaryChange
    {
        internal StagedVoluntaryChange(
            OrderChange change,
            OrderSegment segment,
            OrderService service,
            long replacedOrderServiceId,
            long electronicTicketId,
            long ticketCouponId)
        {
            Change = change;
            Segment = segment;
            Service = service;
            ReplacedOrderServiceId = replacedOrderServiceId;
            ElectronicTicketId = electronicTicketId;
            TicketCouponId = ticketCouponId;
        }

        public long ReplacedOrderServiceId { get; }

        public long ElectronicTicketId { get; }

        public long TicketCouponId { get; }

        public long ReplacementOrderServiceId => Service.Id;

        public long ReplacementOrderSegmentId => Segment.Id;

        internal OrderChange Change { get; }

        internal OrderSegment Segment { get; }

        internal OrderService Service { get; }
    }
}
