using AeroTech.Ordering.Domain.Ports.DocumentVoid;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentVoid.Services
{
    public sealed class UnconfiguredDocumentVoidProvider : IDocumentVoidPort
    {
        public Task<DocumentVoidEligibility> CheckEligibilityAsync(
            DocumentVoidEligibilityRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentVoidSourceNotConfigured();

        public Task<DocumentVoidResult> VoidAsync(
            DocumentVoidRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentVoidSourceNotConfigured();

        public Task<DocumentVoidResult> RecoverAsync(
            DocumentVoidRecoveryRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.DocumentVoidSourceNotConfigured();
    }
}
