using AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.ManualRefundAuthorization.Services
{
    public sealed class UnconfiguredManualRefundAuthorizationProvider : IManualRefundAuthorizationPort
    {
        public Task<ManualRefundAuthorizationDecision> AuthorizeAsync(
            ManualRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ManualRefundAuthorizationNotConfigured();
    }
}
