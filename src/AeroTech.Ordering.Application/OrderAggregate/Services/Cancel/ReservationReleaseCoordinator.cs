using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.Ports.Reservation;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IReservationReleaseCoordinator
    {
        Task<ProviderOperationOutcome> ReleaseAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);

        Task<ProviderOperationOutcome> RecoverAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);

        Task<bool> HasOutstandingObligationAsync(
            long orderId,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default);
    }

    public sealed class ReservationReleaseCoordinator : IReservationReleaseCoordinator
    {
        public const string ReleaseStep = "release";

        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IReservationPort _reservationPort;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IClock _clock;

        public ReservationReleaseCoordinator(
            IFulfillmentReservationRepository reservations,
            IReservationPort reservationPort,
            IOrderOperationCoordinator operations,
            IClock clock)
        {
            _reservations = reservations;
            _reservationPort = reservationPort;
            _operations = operations;
            _clock = clock;
        }

        public async Task<ProviderOperationOutcome> ReleaseAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default)
        {
            var observed = new List<ProviderOperationOutcome>();

            foreach (var reservation in await InScopeAsync(orderId, scopeServiceIds, cancellationToken))
            {
                var released = ReleasableServiceIds(reservation, scopeServiceIds);

                var result = await _reservationPort.ReleaseAsync(
                    new ReleaseReservationRequest(
                        ReleaseKeyFor(operation, reservation),
                        orderId,
                        operation.OperationId,
                        reservation.ExternalReservationRef,
                        released),
                    cancellationToken);

                Apply(reservation, released, result.Outcome);
                observed.Add(result.Outcome);
            }

            return Aggregate(observed);
        }

        public async Task<ProviderOperationOutcome> RecoverAsync(
            long orderId,
            OrderOperation operation,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default)
        {
            var observed = new List<ProviderOperationOutcome>();

            foreach (var reservation in await InScopeAsync(orderId, scopeServiceIds, cancellationToken))
            {
                var recovery = await _reservationPort.RecoverAsync(
                    new RecoverReservationRequest(
                        ReleaseKeyFor(operation, reservation),
                        orderId,
                        operation.OperationId),
                    cancellationToken);

                Apply(reservation, ReleasableServiceIds(reservation, scopeServiceIds), recovery.Outcome);
                observed.Add(recovery.Outcome);
            }

            return Aggregate(observed);
        }

        public async Task<bool> HasOutstandingObligationAsync(
            long orderId,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken = default)
            => (await InScopeAsync(orderId, scopeServiceIds, cancellationToken)).Count > 0;

        private async Task<IReadOnlyList<FulfillmentReservation>> InScopeAsync(
            long orderId,
            IReadOnlyCollection<long>? scopeServiceIds,
            CancellationToken cancellationToken)
        {
            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);

            return reservations
                .Where(IsReleasable)
                .Where(reservation => scopeServiceIds is null || reservation.Covers(scopeServiceIds))
                .Where(reservation => ReleasableServiceIds(reservation, scopeServiceIds).Count > 0)
                .ToList();
        }

        private static IReadOnlyList<long> ReleasableServiceIds(
            FulfillmentReservation reservation,
            IReadOnlyCollection<long>? scopeServiceIds)
        {
            var outstanding = reservation.OutstandingServiceIds();

            return scopeServiceIds is null
                ? outstanding.ToList()
                : outstanding.Where(scopeServiceIds.Contains).ToList();
        }

        private void Apply(
            FulfillmentReservation reservation,
            IReadOnlyList<long> releasedServiceIds,
            ProviderOperationOutcome outcome)
        {
            switch (outcome)
            {
                case ProviderOperationOutcome.Confirmed:
                    reservation.MarkReleased(releasedServiceIds, _clock);
                    break;

                case ProviderOperationOutcome.Rejected:
                    reservation.RestoreAfterUnreleasedCancellation(_clock);
                    break;

                default:
                    reservation.MarkCancellationPending(_clock);
                    break;
            }
        }

        private static ProviderOperationOutcome Aggregate(IReadOnlyList<ProviderOperationOutcome> observed)
        {
            if (observed.Count == 0)
                return ProviderOperationOutcome.Confirmed;

            var unresolved = observed
                .Where(outcome => outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                .ToList();

            if (unresolved.Count > 0)
                return unresolved.Contains(ProviderOperationOutcome.Unknown)
                    ? ProviderOperationOutcome.Unknown
                    : ProviderOperationOutcome.Pending;

            return observed.Contains(ProviderOperationOutcome.Rejected)
                ? ProviderOperationOutcome.Rejected
                : ProviderOperationOutcome.Confirmed;
        }

        private static bool IsReleasable(FulfillmentReservation reservation)
            => reservation.Status is not (FulfillmentReservationStatus.Released or FulfillmentReservationStatus.Rejected);

        private string ReleaseKeyFor(OrderOperation operation, FulfillmentReservation reservation)
            => _operations.ProviderOperationKey(operation, $"{ReleaseStep}:{reservation.Id}");
    }
}
