using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentRefundAdapter : IDocumentRefundPort
    {
        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public ProviderOperationOutcome RefundOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<string> ObservedEligibilityKeys { get; } = new();

        public List<string> ObservedRefundKeys { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public List<DocumentRefundRequest> ObservedRefundRequests { get; } = new();

        public Task<DocumentRefundEligibility> CheckEligibilityAsync(
            DocumentRefundEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedEligibilityKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundEligibility(Eligibility));
        }

        public Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRefundKeys.Add(request.OperationKey);
            ObservedRefundRequests.Add(request);

            return Task.FromResult(new DocumentRefundResult(RefundOutcome, $"RFND-{request.DocumentNumber}"));
        }

        public Task<DocumentRefundResult> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundResult(
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed ? $"RFND-{request.DocumentNumber}" : null));
        }
    }
}
