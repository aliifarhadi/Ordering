using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanCouponRow
    {
        public long OperationId { get; set; }

        public long PredecessorTicketCouponId { get; set; }

        public int PredecessorCouponNumber { get; set; }

        public long PredecessorOrderServiceId { get; set; }

        public ExchangeCouponDisposition Disposition { get; set; }

        public long SuccessorTicketCouponId { get; set; }

        public long? ReplacementOrderServiceId { get; set; }

        public long? ReplacementOrderSegmentId { get; set; }

        public int? SuccessorCouponNumber { get; set; }
    }
}
