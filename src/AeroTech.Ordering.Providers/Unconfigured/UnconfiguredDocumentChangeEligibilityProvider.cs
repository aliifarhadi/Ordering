using AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Unconfigured
{
    public sealed class UnconfiguredDocumentChangeEligibilityProvider : IDocumentChangeEligibilityPort
    {
        public Task<DocumentChangeEligibility> EvaluateAsync(
            DocumentChangeEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentChangeEligibilitySourceNotConfigured();
    }
}
