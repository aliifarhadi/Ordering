using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record ReservationServiceOutcome(
        long OrderServiceId,
        ReservationMemberStatus Status,
        string? ExternalServiceRef = null,
        string? ObservedBookingClass = null,
        string? ExternalStatus = null,
        DateTimeOffset? ValidUntil = null);
}
