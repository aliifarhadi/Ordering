namespace AeroTech.Ordering.Domain.Ports.Reservation
{
    public interface IReservationPort
    {
        Task<ReservationOutcome> ReserveAsync(ReserveRequest request, CancellationToken cancellationToken = default);

        Task<ReservationOutcome> ReleaseAsync(ReleaseReservationRequest request, CancellationToken cancellationToken = default);

        Task<ReservationOutcome> RecoverAsync(RecoverReservationRequest request, CancellationToken cancellationToken = default);
    }
}
