using AeroTech.Ordering.Domain.Ports.ReservationChange;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ReservationChange
{
    internal static class ReservationChangePortFixture
    {
        public const long OrderId = 9_100_002L;
        public const long OperationId = 9_500_002L;
        public const string OperationKey = "exchange-reservation:9200001:9500002";
        public const string ExternalReservationRef = "PNR-CONTRACT-1";
        public const long TravellerId = 9_600_001L;
        public const long ReplacedOrderServiceId = 9_400_003L;
        public const long ReplacementOrderServiceId = 9_400_103L;
        public const long ReplacementOrderSegmentId = 9_450_103L;
        public const long ReplacementFlightCapacityId = 987_654L;
        public const string ReplacementBookingClass = "Q";

        public static ReservationChangeRequest Request() => new(
            OperationKey,
            OrderId,
            OperationId,
            ExternalReservationRef,
            [
                new ReservationChangeItem(
                    ReplacedOrderServiceId,
                    ReplacementOrderServiceId,
                    ReplacementOrderSegmentId,
                    ReplacementFlightCapacityId,
                    ReplacementBookingClass,
                    TravellerId)
            ]);

        public static ReservationChangeRecoveryRequest Recovery() => new(OperationKey, OrderId, OperationId);

        public static ReservationChangeRecoveryRequest UnknownRecovery()
            => new("exchange-reservation:9200001:0", OrderId, 0L);
    }
}
