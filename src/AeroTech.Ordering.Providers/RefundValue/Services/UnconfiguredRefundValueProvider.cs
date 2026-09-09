using AeroTech.Ordering.Domain.Ports.RefundValue;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.RefundValue.Services
{
    public sealed class UnconfiguredRefundValueProvider : IRefundValuePort
    {
        public Task<RefundValueResult> RequestAsync(
            RefundValueRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundValueSourceNotConfigured();

        public Task<RefundValueResult> RecoverAsync(
            RefundValueRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundValueSourceNotConfigured();
    }
}
