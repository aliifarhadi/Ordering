namespace AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection
{
    public sealed record DocumentRefundCorrectionRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long RefundRecordId);
}
