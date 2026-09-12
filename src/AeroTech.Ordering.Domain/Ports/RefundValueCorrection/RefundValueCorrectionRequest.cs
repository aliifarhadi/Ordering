namespace AeroTech.Ordering.Domain.Ports.RefundValueCorrection
{
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
}
