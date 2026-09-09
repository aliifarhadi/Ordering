using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.RefundValueCorrection
{
    public interface IRefundValueCorrectionPort
    {
        Task<RefundValueCorrectionResult> RequestAsync(
            RefundValueCorrectionRequest request,
            CancellationToken cancellationToken = default);

        Task<RefundValueCorrectionRecovery> RecoverAsync(
            RefundValueCorrectionRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record RefundValueCorrectionRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long RefundRecordId,
        long OriginalRefundOperationId,
        string? OriginalValueMovementReference,
        string DocumentNumber,
        decimal Amount,
        int CurrencyId,
        string ApprovedDisposition,
        string? DispositionReference);

    public sealed record RefundValueCorrectionRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long RefundRecordId);

    public sealed record RefundValueCorrectionResult(
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null);

    public sealed record RefundValueCorrectionRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null);
}
