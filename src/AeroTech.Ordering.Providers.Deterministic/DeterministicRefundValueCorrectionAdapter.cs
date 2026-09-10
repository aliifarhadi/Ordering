using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.RefundValueCorrection;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicRefundValueCorrectionAdapter : IRefundValueCorrectionPort
    {
        public ProviderOperationOutcome RequestOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool RecoveredAsDispatched { get; set; }

        public bool ThrowOnRequest { get; set; }

        public List<RefundValueCorrectionRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<RefundValueCorrectionResult> RequestAsync(
            RefundValueCorrectionRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowOnRequest)
                throw new InvalidOperationException("The refund value correction provider is unreachable.");

            return Task.FromResult(new RefundValueCorrectionResult(
                RequestOutcome,
                RequestOutcome == ProviderOperationOutcome.Confirmed ? $"VALCX-{request.RefundRecordId}" : null));
        }

        public Task<RefundValueCorrectionRecovery> RecoverAsync(
            RefundValueCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new RefundValueCorrectionRecovery(
                RecoveredAsDispatched,
                RecoveryOutcome,
                RecoveredAsDispatched && RecoveryOutcome == ProviderOperationOutcome.Confirmed
                    ? $"VALCX-RECOVERED-{request.RefundRecordId}"
                    : null));
        }
    }
}
