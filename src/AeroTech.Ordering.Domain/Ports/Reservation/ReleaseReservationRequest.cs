namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record ReleaseReservationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalReservationRef,
        IReadOnlyList<long> OrderServiceIds);
}
