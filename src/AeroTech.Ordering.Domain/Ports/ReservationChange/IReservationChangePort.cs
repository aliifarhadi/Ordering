using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public interface IReservationChangePort
    {
        Task<ReservationChangeResult> ApplyAsync(
            ReservationChangeRequest request,
            CancellationToken cancellationToken = default);

        Task<ReservationChangeRecovery> RecoverAsync(
            ReservationChangeRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record ReservationChangeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalReservationRef,
        long ReplacedOrderServiceId,
        long ReplacementOrderServiceId,
        long ReplacementOrderSegmentId,
        long ReplacementFlightCapacityId,
        string? ReplacementBookingClass,
        long TravelerId);

    public sealed record ReservationChangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);

    public sealed record ReservationChangeResult(
        ProviderOperationOutcome Outcome,
        string? ExternalReservationRef = null,
        string? ExternalServiceRef = null,
        string? Detail = null);

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
