using AeroTech.Ordering.Domain.Ports.Reservation;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class DispatchThenFailReservationPort : IReservationPort
    {
        private readonly IReservationPort _provider;

        public DispatchThenFailReservationPort(IReservationPort provider) => _provider = provider;

        public Task<ReservationOutcome> ReserveAsync(ReserveRequest request, CancellationToken cancellationToken = default)
            => _provider.ReserveAsync(request, cancellationToken);

        public async Task<ReservationOutcome> ReleaseAsync(
            ReleaseReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _provider.ReleaseAsync(request, cancellationToken);

            throw new InvalidOperationException("The reservation release response never reached Ordering.");
        }

        public Task<ReservationOutcome> RecoverAsync(RecoverReservationRequest request, CancellationToken cancellationToken = default)
            => _provider.RecoverAsync(request, cancellationToken);
    }
}
