using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicDocumentIssuanceAdapter : IDocumentIssuancePort
    {
        public ProviderOperationOutcome Outcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public List<DocumentIssuanceRequest> Requests { get; } = new();

        public Task<DocumentIssuanceResult> IssueAsync(DocumentIssuanceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(new DocumentIssuanceResult(
                Outcome,
                Outcome == ProviderOperationOutcome.Confirmed ? $"DOC-{request.DocumentNumber}" : null));
        }

        public Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentIssuanceResult(ProviderOperationOutcome.Unknown));
    }
}
