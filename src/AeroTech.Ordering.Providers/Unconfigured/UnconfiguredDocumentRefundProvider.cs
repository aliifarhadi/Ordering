using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredDocumentRefundProvider : IDocumentRefundPort
    {
        public Task<DocumentRefundEligibility> CheckEligibilityAsync(
            DocumentRefundEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundSourceNotConfigured();

        public Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundSourceNotConfigured();

        public Task<DocumentRefundResult> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRefundSourceNotConfigured();
    }
}
