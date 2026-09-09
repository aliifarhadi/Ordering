using AeroTech.Ordering.Domain.Ports.ReservationChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.ReservationChange.Services
{
    public sealed class UnconfiguredReservationChangeProvider : IReservationChangePort
    {
        public Task<ReservationChangeResult> ApplyAsync(
            ReservationChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ReservationChangeSourceNotConfigured();

        public Task<ReservationChangeResult> RecoverAsync(
            ReservationChangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ReservationChangeSourceNotConfigured();
    }
}
