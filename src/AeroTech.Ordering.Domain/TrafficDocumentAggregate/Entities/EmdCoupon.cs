using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities
{
    public sealed class EmdCoupon : DocumentCoupon
    {
        private EmdCoupon()
        {
        }

        internal EmdCoupon(
            long id,
            long trafficDocumentId,
            long orderServiceId,
            long? associatedTicketCouponId,
            int couponNumber,
            DocumentAmounts amounts)
            : base(id, trafficDocumentId, orderServiceId, couponNumber, amounts)
        {
            AssociatedTicketCouponId = associatedTicketCouponId;
        }

        public long? AssociatedTicketCouponId { get; private set; }
    }
}
