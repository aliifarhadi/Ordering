using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.RefundValue;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicRefundValueAdapter : IRefundValuePort
    {
        public ProviderOperationOutcome RequestOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowOnRequest { get; set; }

        public List<RefundValueRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<RefundValueResult> RequestAsync(
            RefundValueRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowOnRequest)
                throw new InvalidOperationException("The refund value movement provider is unreachable.");

            return Task.FromResult(new RefundValueResult(
                RequestOutcome,
                RequestOutcome == ProviderOperationOutcome.Confirmed ? $"VAL-{request.DocumentNumber}" : null));
        }

        public Task<RefundValueResult> RecoverAsync(
            RefundValueRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new RefundValueResult(RecoveryOutcome));
        }
    }
}
