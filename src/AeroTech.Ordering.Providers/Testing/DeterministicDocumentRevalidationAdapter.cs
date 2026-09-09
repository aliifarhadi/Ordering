using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRevalidation;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicDocumentRevalidationAdapter : IDocumentRevalidationPort
    {
        public ProviderOperationOutcome RevalidationOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<DocumentRevalidationRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<DocumentRevalidationResult> RevalidateAsync(
            DocumentRevalidationRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            return Task.FromResult(new DocumentRevalidationResult(
                RevalidationOutcome,
                $"RVAL-{request.DocumentNumber}"));
        }

        public Task<DocumentRevalidationResult> RecoverAsync(
            DocumentRevalidationRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRevalidationResult(
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed ? $"RVAL-{request.DocumentNumber}" : null));
        }
    }
}
