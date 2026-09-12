namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record ReserveRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        DateTimeOffset? HoldUntil,
        IReadOnlyList<ReserveServiceRequest> Services);
}
