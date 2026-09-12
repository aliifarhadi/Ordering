using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record ReservationOutcome(
        ProviderOperationOutcome Outcome,
        string? ExternalReservationRef,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<ReservationServiceOutcome> Services,
        string? Detail = null);
}
