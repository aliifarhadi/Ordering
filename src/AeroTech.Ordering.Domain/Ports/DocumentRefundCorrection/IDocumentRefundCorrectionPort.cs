using AeroTech.Messages.Ordering.Enums;

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

    public sealed record DocumentRefundCorrectionEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long RefundRecordId,
        string OriginalRefundReference,
        IReadOnlyList<int> CouponNumbers);

    public sealed record DocumentRefundCorrectionEligibility(EligibilityOutcome Outcome, string? Detail = null);

    public sealed record DocumentRefundCorrectionRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long RefundRecordId,
        string OriginalRefundReference,
        IReadOnlyList<int> CouponNumbers,
        string Reason,
        string? ReasonDetail);

    public sealed record DocumentRefundCorrectionRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long RefundRecordId);

    public sealed record DocumentRefundCorrectionResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null);
}
