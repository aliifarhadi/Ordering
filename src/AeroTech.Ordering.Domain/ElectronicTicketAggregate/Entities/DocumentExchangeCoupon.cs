using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentExchangeCoupon : Entity<long>
    {
        private DocumentExchangeCoupon()
        {
        }

        internal DocumentExchangeCoupon(
            long id,
            long documentExchangeRecordId,
            long predecessorTicketCouponId,
            int predecessorCouponNumber,
            long successorTicketCouponId,
            int successorCouponNumber,
            long previousOrderServiceId,
            long successorOrderServiceId)
        {
            Id = id;
            DocumentExchangeRecordId = documentExchangeRecordId;
            PredecessorTicketCouponId = predecessorTicketCouponId;
            PredecessorCouponNumber = predecessorCouponNumber;
            SuccessorTicketCouponId = successorTicketCouponId;
            SuccessorCouponNumber = successorCouponNumber;
            PreviousOrderServiceId = previousOrderServiceId;
            SuccessorOrderServiceId = successorOrderServiceId;
        }

        public long DocumentExchangeRecordId { get; private set; }

        public long PredecessorTicketCouponId { get; private set; }

        public int PredecessorCouponNumber { get; private set; }

        public long SuccessorTicketCouponId { get; private set; }

        public int SuccessorCouponNumber { get; private set; }

        public long PreviousOrderServiceId { get; private set; }

        public long SuccessorOrderServiceId { get; private set; }
    }
}
