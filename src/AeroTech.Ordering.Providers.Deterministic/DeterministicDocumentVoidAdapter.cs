using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentVoid;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicDocumentVoidAdapter : IDocumentVoidPort
    {
        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public bool RefundRequiredInstead { get; set; }

        public ProviderOperationOutcome VoidOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<string> ObservedEligibilityKeys { get; } = new();

        public List<string> ObservedVoidKeys { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public Task<DocumentVoidEligibility> CheckEligibilityAsync(
            DocumentVoidEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedEligibilityKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentVoidEligibility(Eligibility, RefundRequiredInstead));
        }

        public Task<DocumentVoidResult> VoidAsync(
            DocumentVoidRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedVoidKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentVoidResult(VoidOutcome, $"VOID-{request.DocumentNumber}"));
        }

        public Task<DocumentVoidResult> RecoverAsync(
            DocumentVoidRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentVoidResult(RecoveryOutcome));
        }
    }
}
