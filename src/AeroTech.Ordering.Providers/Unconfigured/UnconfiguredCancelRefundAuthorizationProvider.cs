using AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredCancelRefundAuthorizationProvider : ICancelRefundAuthorizationPort
    {
        public Task<CancelRefundAuthorizationDecision> AuthorizeAsync(
            CancelRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.CancelRefundAuthorizationNotConfigured();
    }
}
