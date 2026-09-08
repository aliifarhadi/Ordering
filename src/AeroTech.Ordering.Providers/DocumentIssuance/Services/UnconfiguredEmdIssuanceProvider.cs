using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.DocumentIssuance.Services
{
    public sealed class UnconfiguredEmdIssuanceProvider : IEmdIssuancePort
    {
        public Task<DocumentIssuanceResult> IssueAsync(EmdIssuanceRequest request, CancellationToken cancellationToken = default)
            => throw ExceptionFactory.MiscellaneousDocumentSourceNotConfigured();

        public Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default)
            => throw ExceptionFactory.MiscellaneousDocumentSourceNotConfigured();
    }
}
