using AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentRefundCorrection.Services
{
    public sealed class UnconfiguredDocumentRefundCorrectionProvider : IDocumentRefundCorrectionPort
    {
        public Task<DocumentRefundCorrectionEligibility> CheckEligibilityAsync(
            DocumentRefundCorrectionEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundCorrectionSourceNotConfigured();

        public Task<DocumentRefundCorrectionResult> CancelRefundAsync(
            DocumentRefundCorrectionRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundCorrectionSourceNotConfigured();

        public Task<DocumentRefundCorrectionResult> RecoverAsync(
            DocumentRefundCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundCorrectionSourceNotConfigured();
    }
}
