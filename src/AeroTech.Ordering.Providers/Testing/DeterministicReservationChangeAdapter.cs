using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ReservationChange;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicReservationChangeAdapter : IReservationChangePort
    {
        public ProviderOperationOutcome ApplyOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<ReservationChangeRequest> ObservedApplies { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<ReservationChangeResult> ApplyAsync(
            ReservationChangeRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedApplies.Add(request);

            return Task.FromResult(new ReservationChangeResult(
                ApplyOutcome,
                $"PNR-CHG-{request.OperationId}",
                $"SEG-{request.ReplacementOrderServiceId}"));
        }

        public Task<ReservationChangeResult> RecoverAsync(
            ReservationChangeRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new ReservationChangeResult(
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed ? $"PNR-CHG-{request.OperationId}" : null));
        }
    }
}
