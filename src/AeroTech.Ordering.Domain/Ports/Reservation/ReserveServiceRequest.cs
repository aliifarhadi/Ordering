namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record ReserveServiceRequest(
        long OrderServiceId,
        long TravelerId,
        long JourneySegmentId,
        long FlightCapacityId,
        string? BookingClass);
}
