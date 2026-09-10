using AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentChange.Services
{
    public sealed class UnconfiguredDocumentChangeEligibilityProvider : IDocumentChangeEligibilityPort
    {
        public Task<DocumentChangeEligibility> EvaluateAsync(
            DocumentChangeEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentChangeEligibilitySourceNotConfigured();
    }
}
