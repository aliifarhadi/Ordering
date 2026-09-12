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
}
