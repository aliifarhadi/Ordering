using AeroTech.Ordering.Domain.Ports.DocumentRevalidation;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentRevalidation.Services
{
    public sealed class UnconfiguredDocumentRevalidationProvider : IDocumentRevalidationPort
    {
        public Task<DocumentRevalidationResult> RevalidateAsync(
            DocumentRevalidationRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRevalidationSourceNotConfigured();

        public Task<DocumentRevalidationRecovery> RecoverAsync(
            DocumentRevalidationRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentRevalidationSourceNotConfigured();
    }
}
