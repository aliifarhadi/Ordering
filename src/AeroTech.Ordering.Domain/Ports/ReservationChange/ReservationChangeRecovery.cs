using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public sealed record ReservationChangeRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ExternalReservationRef = null,
        string? ExternalServiceRef = null,
        string? Detail = null)
    {
        public ReservationChangeResult AsResult()
            => new(Outcome, ExternalReservationRef, ExternalServiceRef, Detail);
    }
}
