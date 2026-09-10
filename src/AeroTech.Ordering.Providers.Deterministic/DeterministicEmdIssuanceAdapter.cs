using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicEmdIssuanceAdapter : IEmdIssuancePort
    {
        private readonly Dictionary<string, ProviderOperationOutcome> _numberOutcomes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProviderOperationOutcome> _numberRecoveries = new(StringComparer.Ordinal);

        public ProviderOperationOutcome Outcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public List<EmdIssuanceRequest> Requests { get; } = new();

        public List<DocumentRecoveryRequest> Recoveries { get; } = new();

        public void OutcomeForDocument(string documentNumber, ProviderOperationOutcome outcome)
            => _numberOutcomes[documentNumber] = outcome;

        public void RecoveryForDocument(string documentNumber, ProviderOperationOutcome outcome)
            => _numberRecoveries[documentNumber] = outcome;

        public Task<DocumentIssuanceResult> IssueAsync(EmdIssuanceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            var outcome = _numberOutcomes.TryGetValue(request.DocumentNumber, out var scripted) ? scripted : Outcome;

            return Task.FromResult(Result(outcome, request.DocumentNumber));
        }

        public Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default)
        {
            Recoveries.Add(request);

            var outcome = _numberRecoveries.TryGetValue(request.DocumentNumber, out var scripted) ? scripted : RecoveryOutcome;

            return Task.FromResult(Result(outcome, request.DocumentNumber));
        }

        private static DocumentIssuanceResult Result(ProviderOperationOutcome outcome, string documentNumber)
            => new(
                outcome,
                outcome == ProviderOperationOutcome.Confirmed ? $"EMD-{documentNumber}" : null,
                outcome == ProviderOperationOutcome.Confirmed ? null : outcome.ToString());
    }
}
