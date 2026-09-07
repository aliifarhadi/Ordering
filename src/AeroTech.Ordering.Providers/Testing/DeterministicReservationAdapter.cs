using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.Reservation;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicReservationAdapter : IReservationPort
    {
        private readonly Dictionary<string, ReservationOutcome> _replies = new(StringComparer.Ordinal);

        public ReservationMemberStatus DefaultMemberStatus { get; set; } = ReservationMemberStatus.Confirmed;

        public ProviderOperationOutcome DefaultOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome ReleaseOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public List<string> ObservedOperationKeys { get; } = new();

        public void ReplyTo(string operationKey, ReservationOutcome outcome) => _replies[operationKey] = outcome;

        public Task<ReservationOutcome> ReserveAsync(ReserveRequest request, CancellationToken cancellationToken = default)
        {
            ObservedOperationKeys.Add(request.OperationKey);

            if (_replies.TryGetValue(request.OperationKey, out var scripted))
                return Task.FromResult(scripted);

            return Task.FromResult(new ReservationOutcome(
                DefaultOutcome,
                $"PNR-{request.OperationId}",
                request.HoldUntil,
                request.Services
                    .Select(service => new ReservationServiceOutcome(
                        service.OrderServiceId,
                        DefaultMemberStatus,
                        $"SEG-{service.OrderServiceId}",
                        service.BookingClass))
                    .ToList()));
        }

        public Task<ReservationOutcome> ReleaseAsync(ReleaseReservationRequest request, CancellationToken cancellationToken = default)
        {
            ObservedOperationKeys.Add(request.OperationKey);

            return Task.FromResult(new ReservationOutcome(
                ReleaseOutcome,
                request.ExternalReservationRef,
                null,
                request.OrderServiceIds
                    .Select(id => new ReservationServiceOutcome(id, ReservationMemberStatus.Released))
                    .ToList()));
        }

        public Task<ReservationOutcome> RecoverAsync(RecoverReservationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReservationOutcome(ProviderOperationOutcome.Unknown, null, null, []));
    }
}
