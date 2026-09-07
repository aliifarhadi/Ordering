using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities
{
    public sealed class TicketCoupon : DocumentCoupon
    {
        private TicketCoupon()
        {
        }

        internal TicketCoupon(
            long id,
            long trafficDocumentId,
            long orderServiceId,
            long orderSegmentId,
            int couponNumber,
            DocumentAmounts amounts)
            : base(id, trafficDocumentId, orderServiceId, couponNumber, amounts)
        {
            OrderSegmentId = orderSegmentId;
        }

        public long OrderSegmentId { get; private set; }
    }
}
