using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentChangeEligibilityAdapter : IDocumentChangeEligibilityPort
    {
        public DocumentChangeEligibilityOutcome Outcome { get; set; } = DocumentChangeEligibilityOutcome.Revalidate;

        public string? Detail { get; set; }

        public List<DocumentChangeEligibilityRequest> ObservedRequests { get; } = new();

        public Task<DocumentChangeEligibility> EvaluateAsync(
            DocumentChangeEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            return Task.FromResult(new DocumentChangeEligibility(Outcome, Detail));
        }
    }
}
