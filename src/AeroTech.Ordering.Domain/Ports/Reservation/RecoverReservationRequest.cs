namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public sealed record RecoverReservationRequest(
        string OperationKey,
        long OrderId,
        long OperationId);
}
