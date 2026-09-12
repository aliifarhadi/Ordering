namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public sealed record ReservationChangeItem(
        long ReplacedOrderServiceId,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long ReplacementFlightCapacityId,
        string? ReplacementBookingClass,
        long TravelerId,
        string? ExternalServiceRef = null);
}
