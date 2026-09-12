namespace AeroTech.Ordering.Domain.Ports.RefundValueCorrection
{
    public sealed record RefundValueCorrectionRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long RefundRecordId);
}
