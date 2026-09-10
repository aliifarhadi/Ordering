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

        public int SegmentMarketingAirlineId { get; set; }

        public string SegmentFlightNumber { get; set; } = null!;

        public int SegmentOriginAirportId { get; set; }

        public int SegmentDestinationAirportId { get; set; }

        public DateTimeOffset SegmentDepartureDateTime { get; set; }

        public DateTimeOffset SegmentArrivalDateTime { get; set; }

        public string? SegmentBookingClass { get; set; }
    }
}
