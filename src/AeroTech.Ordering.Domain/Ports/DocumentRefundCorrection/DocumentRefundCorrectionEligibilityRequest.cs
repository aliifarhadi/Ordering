namespace AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection
{
    public sealed record DocumentRefundCorrectionEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long RefundRecordId,
        string OriginalRefundReference,
        IReadOnlyList<int> CouponNumbers);
}
