namespace AeroTech.Ordering.Domain.Ports.ReservationChange
{
    public sealed record ReservationChangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);
}
