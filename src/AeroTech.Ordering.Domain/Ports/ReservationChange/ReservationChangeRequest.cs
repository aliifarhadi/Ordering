namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public sealed record ReservationChangeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalReservationRef,
        IReadOnlyList<ReservationChangeItem> Items);
}
