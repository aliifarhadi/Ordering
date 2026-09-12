namespace AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection
{
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
}
