using AeroTech.Ordering.Domain.Ports.RefundValueCorrection;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredRefundValueCorrectionProvider : IRefundValueCorrectionPort
    {
        public Task<RefundValueCorrectionResult> RequestAsync(
            RefundValueCorrectionRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundValueCorrectionSourceNotConfigured();

        public Task<RefundValueCorrectionRecovery> RecoverAsync(
            RefundValueCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundValueCorrectionSourceNotConfigured();
    }
}
