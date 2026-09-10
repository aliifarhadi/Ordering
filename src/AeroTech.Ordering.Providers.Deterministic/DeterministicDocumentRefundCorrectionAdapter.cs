using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentRefundCorrectionAdapter : IDocumentRefundCorrectionPort
    {
        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public ProviderOperationOutcome CorrectionOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public List<string> ObservedEligibilityKeys { get; } = new();

        public List<string> ObservedCorrectionKeys { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public List<DocumentRefundCorrectionRequest> ObservedCorrectionRequests { get; } = new();

        public Task<DocumentRefundCorrectionEligibility> CheckEligibilityAsync(
            DocumentRefundCorrectionEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedEligibilityKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundCorrectionEligibility(Eligibility));
        }

        public Task<DocumentRefundCorrectionResult> CancelRefundAsync(
            DocumentRefundCorrectionRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedCorrectionKeys.Add(request.OperationKey);
            ObservedCorrectionRequests.Add(request);

            return Task.FromResult(new DocumentRefundCorrectionResult(
                CorrectionOutcome,
                $"CXRFND-{request.DocumentNumber}-{request.RefundRecordId}"));
        }

        public Task<DocumentRefundCorrectionResult> RecoverAsync(
            DocumentRefundCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            return Task.FromResult(new DocumentRefundCorrectionResult(
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed
                    ? $"CXRFND-{request.DocumentNumber}-{request.RefundRecordId}"
                    : null));
        }
    }
}
