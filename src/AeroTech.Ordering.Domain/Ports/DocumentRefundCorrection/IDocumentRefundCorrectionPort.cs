namespace AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection
{
    public interface IDocumentRefundCorrectionPort
    {
        Task<DocumentRefundCorrectionEligibility> CheckEligibilityAsync(
            DocumentRefundCorrectionEligibilityRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundCorrectionResult> CancelRefundAsync(
            DocumentRefundCorrectionRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundCorrectionResult> RecoverAsync(
            DocumentRefundCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
