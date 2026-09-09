using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentRefundCorrectionCoupon : Entity<long>
    {
        private DocumentRefundCorrectionCoupon()
        {
        }

        internal DocumentRefundCorrectionCoupon(
            long id,
            long documentRefundCorrectionRecordId,
            long ticketCouponId,
            int couponNumber,
            long orderServiceId)
        {
            Id = id;
            DocumentRefundCorrectionRecordId = documentRefundCorrectionRecordId;
            TicketCouponId = ticketCouponId;
            CouponNumber = couponNumber;
            OrderServiceId = orderServiceId;
        }

        public long DocumentRefundCorrectionRecordId { get; private set; }

        public long TicketCouponId { get; private set; }

        public int CouponNumber { get; private set; }

        public long OrderServiceId { get; private set; }
    }
}
