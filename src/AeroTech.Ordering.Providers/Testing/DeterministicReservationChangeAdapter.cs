using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ReservationChange;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicReservationChangeAdapter : IReservationChangePort
    {
        private readonly HashSet<string> _dispatched = new(StringComparer.Ordinal);

        public ProviderOperationOutcome ApplyOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public List<ReservationChangeRequest> ObservedApplies { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched;

        public Task<ReservationChangeResult> ApplyAsync(
            ReservationChangeRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedApplies.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The reservation change request never left Ordering.");

            _dispatched.Add(request.OperationKey);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The reservation change response never reached Ordering.");

            return Task.FromResult(new ReservationChangeResult(
                ApplyOutcome,
                $"PNR-CHG-{request.OperationId}",
                $"SEG-{request.ReplacementOrderServiceId}"));
        }

        public Task<ReservationChangeRecovery> RecoverAsync(
            ReservationChangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The reservation change provider is unreachable.");

            if (!_dispatched.Contains(request.OperationKey))
                return Task.FromResult(new ReservationChangeRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such reservation change operation"));

            return Task.FromResult(new ReservationChangeRecovery(
                true,
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed ? $"PNR-CHG-{request.OperationId}" : null));
        }
    }
}
