using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record ServicingReservationEvidence(
        long FulfillmentReservationId,
        long OperationId,
        string? ExternalReservationRef,
        FulfillmentReservationStatus Status,
        long OrderServiceId,
        string? ExternalServiceRef,
        ReservationMemberStatus ObservedStatus,
        string? ExternalStatus)
    {
        public bool IsObservationUnresolved
            => ObservedStatus is ReservationMemberStatus.Unknown or ReservationMemberStatus.Pending;
    }
}
