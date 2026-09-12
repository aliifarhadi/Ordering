using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public sealed record ReservationChangeResult(
        ProviderOperationOutcome Outcome,
        string? ExternalReservationRef = null,
        string? ExternalServiceRef = null,
        string? Detail = null);
}
