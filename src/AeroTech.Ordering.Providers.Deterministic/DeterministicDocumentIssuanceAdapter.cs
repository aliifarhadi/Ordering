using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentIssuanceAdapter : IDocumentIssuancePort
    {
        private readonly Dictionary<long, ProviderOperationOutcome> _travelerOutcomes = new();
        private readonly Dictionary<long, ProviderOperationOutcome> _travelerRecoveries = new();

        public ProviderOperationOutcome Outcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<DocumentIssuanceRequest> Requests { get; } = new();

        public List<DocumentRecoveryRequest> Recoveries { get; } = new();

        public void OutcomeForTraveler(long travelerId, ProviderOperationOutcome outcome)
            => _travelerOutcomes[travelerId] = outcome;

        public void RecoveryForTraveler(long travelerId, ProviderOperationOutcome outcome)
            => _travelerRecoveries[travelerId] = outcome;

        public Task<DocumentIssuanceResult> IssueAsync(DocumentIssuanceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            var outcome = _travelerOutcomes.TryGetValue(request.TravelerId, out var scripted) ? scripted : Outcome;

            return Task.FromResult(new DocumentIssuanceResult(
                outcome,
                outcome == ProviderOperationOutcome.Confirmed ? $"DOC-{request.DocumentNumber}" : null,
                outcome == ProviderOperationOutcome.Confirmed ? null : outcome.ToString()));
        }

        public Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default)
        {
            Recoveries.Add(request);

            var travelerId = TravelerFromKey(request.OperationKey);

            var outcome = travelerId is { } id && _travelerRecoveries.TryGetValue(id, out var scripted)
                ? scripted
                : RecoveryOutcome;

            return Task.FromResult(new DocumentIssuanceResult(
                outcome,
                outcome == ProviderOperationOutcome.Confirmed ? $"DOC-{request.DocumentNumber}" : null,
                outcome == ProviderOperationOutcome.Confirmed ? null : outcome.ToString()));
        }

        private static long? TravelerFromKey(string operationKey)
        {
            var segments = operationKey.Split(':');

            return segments.Length >= 3 && long.TryParse(segments[1], out var travelerId)
                ? travelerId
                : null;
        }
    }
}
