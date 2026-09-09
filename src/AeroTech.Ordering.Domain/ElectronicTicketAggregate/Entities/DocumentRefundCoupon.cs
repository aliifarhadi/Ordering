using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentRefundCoupon : Entity<long>
    {
        private DocumentRefundCoupon()
        {
        }

        internal DocumentRefundCoupon(
            long id,
            long documentRefundRecordId,
            long ticketCouponId,
            int couponNumber,
            long orderServiceId)
        {
            Id = id;
            DocumentRefundRecordId = documentRefundRecordId;
            TicketCouponId = ticketCouponId;
            CouponNumber = couponNumber;
            OrderServiceId = orderServiceId;
        }

        public long DocumentRefundRecordId { get; private set; }

        public long TicketCouponId { get; private set; }

        public int CouponNumber { get; private set; }

        public long OrderServiceId { get; private set; }
    }
}
