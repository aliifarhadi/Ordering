using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public interface IReservationPort
    {
        Task<ReservationOutcome> ReserveAsync(ReserveRequest request, CancellationToken cancellationToken = default);

        Task<ReservationOutcome> ReleaseAsync(ReleaseReservationRequest request, CancellationToken cancellationToken = default);

        Task<ReservationOutcome> RecoverAsync(RecoverReservationRequest request, CancellationToken cancellationToken = default);
    }

    public sealed record ReserveRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        DateTimeOffset? HoldUntil,
        IReadOnlyList<ReserveServiceRequest> Services);

    public sealed record ReserveServiceRequest(
        long OrderServiceId,
        long TravelerId,
        long JourneySegmentId,
        long FlightCapacityId,
        string? BookingClass);

    public sealed record ReleaseReservationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalReservationRef,
        IReadOnlyList<long> OrderServiceIds);

    public sealed record RecoverReservationRequest(
        string OperationKey,
        long OrderId,
        long OperationId);

    public sealed record ReservationOutcome(
        ProviderOperationOutcome Outcome,
        string? ExternalReservationRef,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<ReservationServiceOutcome> Services,
        string? Detail = null);

    public sealed record ReservationServiceOutcome(
        long OrderServiceId,
        ReservationMemberStatus Status,
        string? ExternalServiceRef = null,
        string? ObservedBookingClass = null,
        string? ExternalStatus = null,
        DateTimeOffset? ValidUntil = null);
}
