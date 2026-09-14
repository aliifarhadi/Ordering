using AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class DispatchThenFailDocumentRefundCorrectionPort : IDocumentRefundCorrectionPort
    {
        private readonly IDocumentRefundCorrectionPort _provider;

        public DispatchThenFailDocumentRefundCorrectionPort(IDocumentRefundCorrectionPort provider) => _provider = provider;

        public Task<DocumentRefundCorrectionEligibility> CheckEligibilityAsync(
            DocumentRefundCorrectionEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => _provider.CheckEligibilityAsync(request, cancellationToken);

        public async Task<DocumentRefundCorrectionResult> CancelRefundAsync(
            DocumentRefundCorrectionRequest request,
            CancellationToken cancellationToken = default)
        {
            await _provider.CancelRefundAsync(request, cancellationToken);

            throw new InvalidOperationException("The document refund correction response never reached Ordering.");
        }

        public Task<DocumentRefundCorrectionResult> RecoverAsync(
            DocumentRefundCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => _provider.RecoverAsync(request, cancellationToken);
    }
}
